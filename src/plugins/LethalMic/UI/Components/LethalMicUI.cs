using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using System.Reflection;
using UnityEngine.UI;
using TMPro;
using UIButton = UnityEngine.UI.Button;
using UISlider = UnityEngine.UI.Slider;
using UIToggle = UnityEngine.UI.Toggle;
using UIImage = UnityEngine.UI.Image;
using LethalMic;

namespace LethalMic.UI.Components
{
    /// <summary>
    /// In-game UI system for LethalMic settings and microphone visualization
    /// </summary>
    public class LethalMicUI : MonoBehaviour
    {
        private static readonly ManualLogSource _logger = BepInEx.Logging.Logger.CreateLogSource("LethalMicUI");
        private bool _isInitialized;
        private bool _isVisible = false;
        private bool _isCalibrating = false;
        private ConfigFile _config;
        
        // Microphone visualization
        private float[] _micLevels = new float[100];
        private int _micLevelIndex = 0;
        private float _currentMicLevel = 0f;
        private float _peakMicLevel = 0f;
        private float _noiseFloor = 0f;
        
        // Performance monitoring
        private float _cpuUsage = 0f;
        
        // UI Elements
        private GameObject _uiRoot;
        private Canvas _canvas;
        private CanvasScaler _scaler;
        private GraphicRaycaster _raycaster;
        private TextMeshProUGUI _micStatusText;
        private TextMeshProUGUI _micLevelText;
        private TextMeshProUGUI _noiseFloorText;
        private TextMeshProUGUI _cpuUsageText;
        private UISlider _gainSlider;
        private UISlider _thresholdSlider;
        private UIToggle _noiseGateToggle;
        private UIButton _calibrateButton;
        private UIImage _levelMeterBackground;
        private UIImage _levelMeterFill;
        private UIImage _peakMeterIndicator;
        private TextMeshProUGUI _noInputWarningText;
        
        // Lethal Company style colors
        private readonly Color _lcBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        private readonly Color _lcTextColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        private readonly Color _lcAccentColor = new Color(0.2f, 0.6f, 0.2f, 1f);
        private readonly Color _lcWarningColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        
        private float _lastUILogTime = 0f;
        private float _uiLogInterval = 1.0f;
        
        // Add new UI element fields
        private UIToggle _compressionToggle;
        private UISlider _compressionRatioSlider;
        private UISlider _attackTimeSlider;
        private UISlider _releaseTimeSlider;
        
        public bool IsVisible => _isVisible;
        
        public void Initialize(ManualLogSource logger, ConfigFile config)
        {
            try
            {
                if (_isInitialized)
                {
                    _logger.LogWarning("LethalMicUI instance already exists, destroying duplicate");
                    Destroy(gameObject);
                    return;
                }

                if (logger == null) throw new ArgumentNullException(nameof(logger));
                if (config == null) throw new ArgumentNullException(nameof(config));
                _config = config;
                _logger.LogInfo("Initializing LethalMicUI...");
                // Do not create UI root or panels yet. Wait for ToggleVisibility.
                _isInitialized = true;
                _logger.LogInfo("LethalMicUI initialized successfully (deferred UI creation)");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing UI: {ex}");
                throw;
            }
        }
        
        private void EnsureUIRoot()
        {
            if (_uiRoot != null) return;
            _uiRoot = new GameObject("LethalMicUI");
            _uiRoot.transform.SetParent(transform, false);
            _canvas = _uiRoot.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            _scaler = _uiRoot.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920, 1080);
            _raycaster = _uiRoot.AddComponent<GraphicRaycaster>();
            CreateMainPanel();
            LoadSettings();
            _uiRoot.SetActive(false);
        }
        
        private void CreateMainPanel()
        {
            var mainPanel = new GameObject("MainPanel");
            mainPanel.transform.SetParent(_uiRoot.transform, false);
            var mainPanelRect = mainPanel.AddComponent<RectTransform>();
            mainPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            mainPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            mainPanelRect.pivot = new Vector2(0.5f, 0.5f); // Center
            mainPanelRect.anchoredPosition = Vector2.zero;

            // Create background panel
            var backgroundObj = new GameObject("Background");
            backgroundObj.transform.SetParent(mainPanel.transform, false);
            var backgroundImage = backgroundObj.AddComponent<UnityEngine.UI.Image>();
            backgroundImage.color = _lcBackgroundColor;
            var backgroundRect = backgroundImage.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f); // Center
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.sizeDelta = new Vector2(400, 0); // Width fixed, height dynamic

            // Add layout group and fitter
            var layout = backgroundObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 25f; // Even spacing for all children
            layout.padding = new RectOffset(20, 20, 20, 20);

            var fitter = backgroundObj.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Now, for each UI element, just create and parent to backgroundObj
            // Status text
            var statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(backgroundObj.transform, false);
            _micStatusText = statusObj.AddComponent<TextMeshProUGUI>();
            _micStatusText.text = "Microphone: Not Connected";
            _micStatusText.fontSize = 24;
            _micStatusText.alignment = TextAlignmentOptions.Center;
            _micStatusText.color = _lcTextColor;

            // Level meter background
            var meterBgObj = new GameObject("LevelMeterBackground");
            meterBgObj.transform.SetParent(backgroundObj.transform, false);
            _levelMeterBackground = meterBgObj.AddComponent<UnityEngine.UI.Image>();
            _levelMeterBackground.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var meterBgRect = _levelMeterBackground.GetComponent<RectTransform>();
            meterBgRect.sizeDelta = new Vector2(320, 24);

            // Border for level meter
            var meterBorderObj = new GameObject("LevelMeterBorder");
            meterBorderObj.transform.SetParent(meterBgObj.transform, false);
            var meterBorderImage = meterBorderObj.AddComponent<UnityEngine.UI.Image>();
            meterBorderImage.color = Color.black; // Border color
            var meterBorderRect = meterBorderObj.GetComponent<RectTransform>();
            meterBorderRect.anchorMin = Vector2.zero;
            meterBorderRect.anchorMax = Vector2.one;
            meterBorderRect.offsetMin = new Vector2(-2, -2);
            meterBorderRect.offsetMax = new Vector2(2, 2);
            meterBorderImage.raycastTarget = false;
            meterBorderImage.type = UnityEngine.UI.Image.Type.Sliced;
            meterBorderImage.pixelsPerUnitMultiplier = 1f;
            meterBgObj.transform.SetAsLastSibling(); // Ensure border is behind fill

            // Level meter fill
            var meterFillObj = new GameObject("LevelMeterFill");
            meterFillObj.transform.SetParent(meterBgObj.transform, false);
            _levelMeterFill = meterFillObj.AddComponent<UnityEngine.UI.Image>();
            _levelMeterFill.color = _lcAccentColor;
            var meterFillRect = _levelMeterFill.GetComponent<RectTransform>();
            meterFillRect.anchorMin = new Vector2(0, 0);
            meterFillRect.anchorMax = new Vector2(0, 1);
            meterFillRect.pivot = new Vector2(0, 0.5f);
            meterFillRect.anchoredPosition = Vector2.zero;
            meterFillRect.sizeDelta = Vector2.zero;

            // Peak indicator
            var peakObj = new GameObject("PeakIndicator");
            peakObj.transform.SetParent(meterBgObj.transform, false);
            _peakMeterIndicator = peakObj.AddComponent<UnityEngine.UI.Image>();
            _peakMeterIndicator.color = _lcWarningColor;
            var peakRect = _peakMeterIndicator.GetComponent<RectTransform>();
            peakRect.anchorMin = new Vector2(0, 0);
            peakRect.anchorMax = new Vector2(0, 1);
            peakRect.pivot = new Vector2(0.5f, 0.5f);
            peakRect.anchoredPosition = Vector2.zero;
            peakRect.sizeDelta = new Vector2(2, 24);

            // Level text
            var levelObj = new GameObject("LevelText");
            levelObj.transform.SetParent(backgroundObj.transform, false);
            _micLevelText = levelObj.AddComponent<TextMeshProUGUI>();
            _micLevelText.text = "Level: 0 dB";
            _micLevelText.fontSize = 20;
            _micLevelText.alignment = TextAlignmentOptions.Center;
            _micLevelText.color = _lcTextColor;

            // Noise floor text
            var noiseFloorObj = new GameObject("NoiseFloorText");
            noiseFloorObj.transform.SetParent(backgroundObj.transform, false);
            _noiseFloorText = noiseFloorObj.AddComponent<TextMeshProUGUI>();
            _noiseFloorText.text = "Noise Floor: -60 dB";
            _noiseFloorText.fontSize = 20;
            _noiseFloorText.alignment = TextAlignmentOptions.Center;
            _noiseFloorText.color = _lcTextColor;

            // CPU usage text
            var cpuObj = new GameObject("CPUUsageText");
            cpuObj.transform.SetParent(backgroundObj.transform, false);
            _cpuUsageText = cpuObj.AddComponent<TextMeshProUGUI>();
            _cpuUsageText.text = "CPU: 0%";
            _cpuUsageText.fontSize = 20;
            _cpuUsageText.alignment = TextAlignmentOptions.Center;
            _cpuUsageText.color = _lcTextColor;

            // Warning text for no input
            var warningObj = new GameObject("NoInputWarningText");
            warningObj.transform.SetParent(backgroundObj.transform, false);
            _noInputWarningText = warningObj.AddComponent<TextMeshProUGUI>();
            _noInputWarningText.text = "";
            _noInputWarningText.fontSize = 18;
            _noInputWarningText.color = _lcWarningColor;
            _noInputWarningText.alignment = TextAlignmentOptions.Center;

            // Settings section
            CreateSettingsSection(backgroundObj.transform);
        }
        
        private void CreateSettingsSection(Transform parent)
        {
            // Gain slider
            CreateSlider("Gain", 0.1f, 10f, _config != null ? _config.Bind("Audio", "Gain", 1.0f, "Microphone gain").Value : 1f, OnGainChanged, out _gainSlider, parent);
            // Threshold slider
            CreateSlider("Threshold", 0f, 1f, _config != null ? _config.Bind("Audio", "Threshold", 0.1f, "Voice activation threshold").Value : 0.1f, OnThresholdChanged, out _thresholdSlider, parent);
            // Noise Gate toggle
            CreateToggle("Noise Gate", _config != null ? _config.Bind("Audio", "NoiseGate", true, "Enable noise gate").Value : true, OnNoiseGateChanged, out _noiseGateToggle, parent);
            // Compression toggle
            CreateToggle("Compression", _config != null ? _config.Bind("Audio", "Compression", true, "Enable audio compression").Value : true, OnCompressionChanged, out _compressionToggle, parent);
            // Compression Ratio slider
            CreateSlider("Compression Ratio", 1f, 20f, _config != null ? _config.Bind("Audio", "CompressionRatio", 4f, "Audio compression ratio").Value : 4f, OnCompressionRatioChanged, out _compressionRatioSlider, parent);
            // Attack Time slider
            CreateSlider("Attack Time", 0f, 100f, _config != null ? _config.Bind("Audio", "AttackTime", 10f, "Compressor attack time (ms)").Value : 10f, OnAttackTimeChanged, out _attackTimeSlider, parent);
            // Release Time slider
            CreateSlider("Release Time", 0f, 1000f, _config != null ? _config.Bind("Audio", "ReleaseTime", 100f, "Compressor release time (ms)").Value : 100f, OnReleaseTimeChanged, out _releaseTimeSlider, parent);
            // Calibrate button
            CreateButton("Calibrate", new Vector2(0, 0), new Vector2(120, 30), OnCalibrateClicked, out _calibrateButton, parent);
        }
        
        private void CreateSlider(string labelText, float minValue, float maxValue, float initialValue, 
            UnityEngine.Events.UnityAction<float> onValueChanged, out UISlider slider, Transform parent)
        {
            var container = new GameObject($"{labelText}Container");
            container.transform.SetParent(parent, false);
            var containerRect = container.AddComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(350, 40);

            // Label
            var labelObj = new GameObject($"{labelText}Label");
            labelObj.transform.SetParent(container.transform, false);
            var labelText_comp = labelObj.AddComponent<TextMeshProUGUI>();
            labelText_comp.text = labelText;
            labelText_comp.fontSize = 18;
            labelText_comp.color = _lcTextColor;
            labelText_comp.alignment = TextAlignmentOptions.Left;
            var labelRect = labelText_comp.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0.5f);
            labelRect.anchorMax = new Vector2(0.4f, 1f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            // Slider
            var sliderObj = new GameObject($"{labelText}Slider");
            sliderObj.transform.SetParent(container.transform, false);
            slider = sliderObj.AddComponent<UISlider>();
            var sliderRect = slider.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.45f, 0.2f);
            sliderRect.anchorMax = new Vector2(0.85f, 0.8f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;

            // Border for slider background
            var sliderBorderObj = new GameObject("SliderBorder");
            sliderBorderObj.transform.SetParent(sliderObj.transform, false);
            var sliderBorderImage = sliderBorderObj.AddComponent<UnityEngine.UI.Image>();
            sliderBorderImage.color = Color.black;
            var sliderBorderRect = sliderBorderObj.GetComponent<RectTransform>();
            sliderBorderRect.anchorMin = Vector2.zero;
            sliderBorderRect.anchorMax = Vector2.one;
            sliderBorderRect.offsetMin = new Vector2(-2, -2);
            sliderBorderRect.offsetMax = new Vector2(2, 2);
            sliderBorderImage.raycastTarget = false;
            sliderBorderImage.type = UnityEngine.UI.Image.Type.Sliced;
            sliderBorderImage.pixelsPerUnitMultiplier = 1f;
            sliderObj.transform.SetAsLastSibling();

            // Background
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            var bgImage = bgObj.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            slider.targetGraphic = bgImage;
            var bgRect = bgImage.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Fill Area
            var fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(sliderObj.transform, false);
            var fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            // Fill
            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);
            var fillImage = fillObj.AddComponent<UnityEngine.UI.Image>();
            fillImage.color = _lcAccentColor;
            var fillRect = fillImage.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            slider.fillRect = fillRect;

            // Handle Slide Area
            var handleAreaObj = new GameObject("Handle Slide Area");
            handleAreaObj.transform.SetParent(sliderObj.transform, false);
            var handleAreaRect = handleAreaObj.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = Vector2.zero;
            handleAreaRect.offsetMax = Vector2.zero;

            // Handle
            var handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(handleAreaObj.transform, false);
            var handleImage = handleObj.AddComponent<UnityEngine.UI.Image>();
            handleImage.color = _lcTextColor;
            var handleRect = handleImage.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(10, 10); // Small handle
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            slider.handleRect = handleRect;

            slider.direction = UISlider.Direction.LeftToRight;
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.value = initialValue;
            slider.onValueChanged.AddListener(onValueChanged);

            // Value Text
            var valueObj = new GameObject($"{labelText}Value");
            valueObj.transform.SetParent(container.transform, false);
            var valueText = valueObj.AddComponent<TextMeshProUGUI>();
            valueText.text = initialValue.ToString("F2");
            valueText.fontSize = 16;
            valueText.color = _lcTextColor;
            valueText.alignment = TextAlignmentOptions.Right;
            var valueRect = valueText.GetComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(0.9f, 0.5f);
            valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;
            slider.onValueChanged.AddListener((value) => valueText.text = value.ToString("F2"));
        }
        
        private void CreateToggle(string labelText, bool initialValue,
            UnityEngine.Events.UnityAction<bool> onValueChanged, out UIToggle toggle, Transform parent)
        {
            var container = new GameObject($"{labelText}Container");
            container.transform.SetParent(parent, false);
            var containerRect = container.AddComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(350, 40);

            // Toggle
            var toggleObj = new GameObject($"{labelText}Toggle");
            toggleObj.transform.SetParent(container.transform, false);
            toggle = toggleObj.AddComponent<UIToggle>();
            var toggleRect = toggle.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0, 0.5f);
            toggleRect.anchorMax = new Vector2(0, 0.5f);
            toggleRect.sizeDelta = new Vector2(28, 28); // Fixed size
            toggleRect.anchoredPosition = new Vector2(20, 0);

            // Background
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(toggleObj.transform, false);
            var bgImage = bgObj.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var bgRect = bgImage.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            toggle.targetGraphic = bgImage;

            // Checkmark
            var checkmarkObj = new GameObject("Checkmark");
            checkmarkObj.transform.SetParent(bgObj.transform, false);
            var checkmarkImage = checkmarkObj.AddComponent<UnityEngine.UI.Image>();
            checkmarkImage.color = _lcAccentColor;
            var checkmarkRect = checkmarkImage.GetComponent<RectTransform>();
            checkmarkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkmarkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkmarkRect.offsetMin = Vector2.zero;
            checkmarkRect.offsetMax = Vector2.zero;
            toggle.graphic = checkmarkImage;

            toggle.isOn = initialValue;
            toggle.onValueChanged.AddListener(onValueChanged);

            // Label
            var labelObj = new GameObject($"{labelText}Label");
            labelObj.transform.SetParent(container.transform, false);
            var labelText_comp = labelObj.AddComponent<TextMeshProUGUI>();
            labelText_comp.text = labelText;
            labelText_comp.fontSize = 18;
            labelText_comp.color = _lcTextColor;
            labelText_comp.alignment = TextAlignmentOptions.Left;
            var labelRect = labelText_comp.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.15f, 0.5f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }
        
        private void CreateButton(string buttonText, Vector2 position, Vector2 size,
            UnityEngine.Events.UnityAction onClick, out UIButton button, Transform parent, bool isCloseButton = false)
        {
            var buttonObj = new GameObject($"{buttonText}Button");
            buttonObj.transform.SetParent(parent, false);
            var buttonRect = buttonObj.AddComponent<RectTransform>();
            button = buttonObj.AddComponent<UIButton>();
            if (isCloseButton)
            {
                buttonRect.anchorMin = new Vector2(1, 1);
                buttonRect.anchorMax = new Vector2(1, 1);
                buttonRect.pivot = new Vector2(1, 1);
            }
            else
            {
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
            }
            buttonRect.anchoredPosition = position;
            buttonRect.sizeDelta = size;
            // Border for button
            var borderObj = new GameObject("ButtonBorder");
            borderObj.transform.SetParent(buttonObj.transform, false);
            var borderImage = borderObj.AddComponent<UnityEngine.UI.Image>();
            borderImage.color = Color.black;
            var borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2, -2);
            borderRect.offsetMax = new Vector2(2, 2);
            borderImage.raycastTarget = false;
            borderImage.type = UnityEngine.UI.Image.Type.Sliced;
            borderImage.pixelsPerUnitMultiplier = 1f;
            buttonObj.transform.SetAsLastSibling();
            var bgImage = buttonObj.AddComponent<UnityEngine.UI.Image>();
            if (bgImage != null)
                bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            var textComp = textObj.AddComponent<TextMeshProUGUI>();
            if (textComp != null)
            {
                textComp.text = buttonText;
                textComp.fontSize = isCloseButton ? 24 : 16;
                textComp.color = _lcTextColor;
                textComp.alignment = TextAlignmentOptions.Center;
                var textRect = textComp.GetComponent<RectTransform>();
                if (textRect != null)
                {
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.offsetMin = Vector2.zero;
                    textRect.offsetMax = Vector2.zero;
                }
            }
            button.targetGraphic = bgImage;
            button.onClick.AddListener(onClick);
        }
        
        private void LoadSettings()
        {
            if (_config == null) return;
            
            var gain = _config.Bind("Audio", "Gain", 1.0f, "Microphone gain").Value;
            var threshold = _config.Bind("Audio", "Threshold", 0.1f, "Voice activation threshold").Value;
            
            if (_gainSlider != null) _gainSlider.value = gain;
            if (_thresholdSlider != null) _thresholdSlider.value = threshold;
        }
        
        private void OnGainChanged(float value)
        {
            LethalMic.LethalMicStatic.SetMicrophoneGain(value);
            LogThrottled($"[UI] GainSlider changed: value={value:F2}");
        }
        
        private void OnThresholdChanged(float value)
        {
            LethalMic.LethalMicStatic.SetNoiseGateThreshold(value);
            LogThrottled($"[UI] ThresholdSlider changed: value={value:F2}");
        }
        
        private void OnNoiseGateChanged(bool value)
        {
            LethalMic.LethalMicStatic.SetNoiseGateEnabled(value);
            LogThrottled($"[UI] Noise Gate changed: {value}");
        }
        
        private void OnCompressionChanged(bool value)
        {
            LethalMic.LethalMicStatic.SetCompressionEnabled(value);
            LogThrottled($"[UI] Compression changed: {value}");
        }
        
        private void OnCompressionRatioChanged(float value)
        {
            LethalMic.LethalMicStatic.SetCompressionRatio(value);
            LogThrottled($"[UI] Compression ratio changed: {value:F1}:1");
        }
        
        private void OnAttackTimeChanged(float value)
        {
            LethalMic.LethalMicStatic.SetAttackTime(value);
            LogThrottled($"[UI] Attack time changed: {value:F0}ms");
        }
        
        private void OnReleaseTimeChanged(float value)
        {
            LethalMic.LethalMicStatic.SetReleaseTime(value);
            LogThrottled($"[UI] Release time changed: {value:F0}ms");
        }
        
        private void OnCalibrateClicked()
        {
            if (_isCalibrating) return;
            StartCoroutine(CalibrateNoiseFloorCoroutine());
        }
        
        private IEnumerator CalibrateNoiseFloorCoroutine()
        {
            _isCalibrating = true;
            _logger.LogInfo("Starting noise floor calibration...");
            
            yield return new WaitForSeconds(3f);
            
            _logger.LogInfo("Noise floor calibration complete");
            _isCalibrating = false;
        }
        
        private void Update()
        {
            if (!_isVisible || !_isInitialized) return;
            
            // Update level meter
            UpdateLevelMeter();
            
            // Update peak indicator
            UpdatePeakIndicator();
            
            // Update text displays
            UpdateTextDisplays();
        }
        
        private void UpdateLevelMeter()
        {
            if (_levelMeterFill != null)
            {
                float normalizedLevel = Mathf.Clamp01(_currentMicLevel);
                _levelMeterFill.rectTransform.anchorMax = new Vector2(normalizedLevel, 1);
            }
            if (_noInputWarningText != null)
            {
                _noInputWarningText.text = _currentMicLevel < 0.001f ? "No input detected! Check your microphone." : "";
            }
        }
        
        private void UpdatePeakIndicator()
        {
            if (_peakMeterIndicator != null)
            {
                float normalizedPeak = Mathf.Clamp01(_peakMicLevel);
                _peakMeterIndicator.rectTransform.anchorMin = new Vector2(normalizedPeak, 0);
                _peakMeterIndicator.rectTransform.anchorMax = new Vector2(normalizedPeak, 1);
            }
        }
        
        private void UpdateTextDisplays()
        {
            if (_micLevelText != null)
            {
                float dbLevel = 20 * Mathf.Log10(Mathf.Max(_currentMicLevel, 0.0001f));
                _micLevelText.text = $"Level: {dbLevel:F1} dB";
            }
            if (_noiseFloorText != null)
            {
                float dbNoiseFloor = 20 * Mathf.Log10(Mathf.Max(_noiseFloor, 0.0001f));
                _noiseFloorText.text = $"Noise Floor: {dbNoiseFloor:F1} dB";
            }
            if (_cpuUsageText != null)
            {
                _cpuUsageText.text = $"CPU: {_cpuUsage:F1}%";
            }
        }
        
        public void UpdateMicStatus(string status, float level)
        {
            if (!_isInitialized) return;
            float dbLevel = 20 * Mathf.Log10(Mathf.Max(level, 0.0001f));
            _currentMicLevel = level;
            _micLevels[_micLevelIndex] = level;
            _micLevelIndex = (_micLevelIndex + 1) % _micLevels.Length;
            _peakMicLevel = Mathf.Max(_peakMicLevel * 0.95f, level);
            if (_micStatusText != null)
            {
                _micStatusText.text = $"Microphone: {status}";
                _micStatusText.color = status == "Connected" ? _lcAccentColor : _lcWarningColor;
            }
        }
        
        public void UpdateCPUUsage(float usage)
        {
            if (!_isInitialized) return;
            _cpuUsage = usage;
        }
        
        public void ToggleVisibility()
        {
            if (!_isInitialized) return;
            EnsureUIRoot();
            _logger.LogInfo($"[UI] ToggleVisibility called. New state: {!_isVisible}");
            _isVisible = !_isVisible;
            _uiRoot.SetActive(_isVisible);
        }
        
        private void OnDestroy()
        {
            if (_uiRoot != null)
            {
                Destroy(_uiRoot);
            }
        }
        
        private void LogThrottled(string message)
        {
            if (Time.time - _lastUILogTime > _uiLogInterval)
            {
                _logger.LogInfo(message);
                _lastUILogTime = Time.time;
            }
        }
    }
}