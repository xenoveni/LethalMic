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
        private UIButton _closeButton;
        private GameObject _settingsPanel;
        private UIImage _levelMeterBackground;
        private UIImage _levelMeterFill;
        private UIImage _peakMeterIndicator;
        
        // Lethal Company style colors
        private readonly Color _lcBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        private readonly Color _lcTextColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        private readonly Color _lcAccentColor = new Color(0.2f, 0.6f, 0.2f, 1f);
        private readonly Color _lcWarningColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        
        private float _lastLoggedMicLevel = float.MinValue;
        private string _lastLoggedStatus = null;
        
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
            CreateSettingsPanel();
            LoadSettings();
            _uiRoot.SetActive(false);
        }
        
        private void CreateMainPanel()
        {
            var mainPanel = new GameObject("MainPanel");
            mainPanel.transform.SetParent(_uiRoot.transform, false);
            
            // Create background panel
            var backgroundObj = new GameObject("Background");
            backgroundObj.transform.SetParent(mainPanel.transform, false);
            var backgroundImage = backgroundObj.AddComponent<UIImage>();
            backgroundImage.color = _lcBackgroundColor;
            
            var backgroundRect = backgroundImage.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.sizeDelta = new Vector2(400, 300);
            
            // Create level meter background
            var meterBgObj = new GameObject("LevelMeterBackground");
            meterBgObj.transform.SetParent(mainPanel.transform, false);
            _levelMeterBackground = meterBgObj.AddComponent<UIImage>();
            _levelMeterBackground.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            var meterBgRect = _levelMeterBackground.GetComponent<RectTransform>();
            meterBgRect.anchorMin = new Vector2(0.5f, 0.5f);
            meterBgRect.anchorMax = new Vector2(0.5f, 0.5f);
            meterBgRect.pivot = new Vector2(0.5f, 0.5f);
            meterBgRect.anchoredPosition = new Vector2(0, 50);
            meterBgRect.sizeDelta = new Vector2(300, 30);
            
            // Create level meter fill
            var meterFillObj = new GameObject("LevelMeterFill");
            meterFillObj.transform.SetParent(meterBgObj.transform, false);
            _levelMeterFill = meterFillObj.AddComponent<UIImage>();
            _levelMeterFill.color = _lcAccentColor;
            
            var meterFillRect = _levelMeterFill.GetComponent<RectTransform>();
            meterFillRect.anchorMin = new Vector2(0, 0);
            meterFillRect.anchorMax = new Vector2(0, 1);
            meterFillRect.pivot = new Vector2(0, 0.5f);
            meterFillRect.anchoredPosition = Vector2.zero;
            meterFillRect.sizeDelta = Vector2.zero;
            
            // Create peak indicator
            var peakObj = new GameObject("PeakIndicator");
            peakObj.transform.SetParent(meterBgObj.transform, false);
            _peakMeterIndicator = peakObj.AddComponent<UIImage>();
            _peakMeterIndicator.color = _lcWarningColor;
            
            var peakRect = _peakMeterIndicator.GetComponent<RectTransform>();
            peakRect.anchorMin = new Vector2(0, 0);
            peakRect.anchorMax = new Vector2(0, 1);
            peakRect.pivot = new Vector2(0.5f, 0.5f);
            peakRect.anchoredPosition = Vector2.zero;
            peakRect.sizeDelta = new Vector2(2, 30);
            
            // Create status text
            var statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(mainPanel.transform, false);
            _micStatusText = statusObj.AddComponent<TextMeshProUGUI>();
            _micStatusText.text = "Microphone: Not Connected";
            _micStatusText.fontSize = 24;
            _micStatusText.alignment = TextAlignmentOptions.Center;
            _micStatusText.color = _lcTextColor;
            
            var statusRect = _micStatusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 0.5f);
            statusRect.anchorMax = new Vector2(0.5f, 0.5f);
            statusRect.pivot = new Vector2(0.5f, 0.5f);
            statusRect.anchoredPosition = new Vector2(0, 100);
            statusRect.sizeDelta = new Vector2(300, 30);
            
            // Create level text
            var levelObj = new GameObject("LevelText");
            levelObj.transform.SetParent(mainPanel.transform, false);
            _micLevelText = levelObj.AddComponent<TextMeshProUGUI>();
            _micLevelText.text = "Level: 0 dB";
            _micLevelText.fontSize = 20;
            _micLevelText.alignment = TextAlignmentOptions.Center;
            _micLevelText.color = _lcTextColor;
            
            var levelRect = _micLevelText.GetComponent<RectTransform>();
            levelRect.anchorMin = new Vector2(0.5f, 0.5f);
            levelRect.anchorMax = new Vector2(0.5f, 0.5f);
            levelRect.pivot = new Vector2(0.5f, 0.5f);
            levelRect.anchoredPosition = new Vector2(0, 0);
            levelRect.sizeDelta = new Vector2(300, 30);
            
            // Create noise floor text
            var noiseFloorObj = new GameObject("NoiseFloorText");
            noiseFloorObj.transform.SetParent(mainPanel.transform, false);
            _noiseFloorText = noiseFloorObj.AddComponent<TextMeshProUGUI>();
            _noiseFloorText.text = "Noise Floor: -60 dB";
            _noiseFloorText.fontSize = 20;
            _noiseFloorText.alignment = TextAlignmentOptions.Center;
            _noiseFloorText.color = _lcTextColor;
            
            var noiseFloorRect = _noiseFloorText.GetComponent<RectTransform>();
            noiseFloorRect.anchorMin = new Vector2(0.5f, 0.5f);
            noiseFloorRect.anchorMax = new Vector2(0.5f, 0.5f);
            noiseFloorRect.pivot = new Vector2(0.5f, 0.5f);
            noiseFloorRect.anchoredPosition = new Vector2(0, -50);
            noiseFloorRect.sizeDelta = new Vector2(300, 30);
            
            // Create CPU usage text
            var cpuObj = new GameObject("CPUUsageText");
            cpuObj.transform.SetParent(mainPanel.transform, false);
            _cpuUsageText = cpuObj.AddComponent<TextMeshProUGUI>();
            _cpuUsageText.text = "CPU: 0%";
            _cpuUsageText.fontSize = 20;
            _cpuUsageText.alignment = TextAlignmentOptions.Center;
            _cpuUsageText.color = _lcTextColor;
            
            var cpuRect = _cpuUsageText.GetComponent<RectTransform>();
            cpuRect.anchorMin = new Vector2(0.5f, 0.5f);
            cpuRect.anchorMax = new Vector2(0.5f, 0.5f);
            cpuRect.pivot = new Vector2(0.5f, 0.5f);
            cpuRect.anchoredPosition = new Vector2(0, -100);
            cpuRect.sizeDelta = new Vector2(300, 30);
        }
        
        private void CreateSettingsPanel()
        {
            if (_uiRoot == null)
            {
                _logger.LogError("[UI] CreateSettingsPanel called but _uiRoot is null!");
                return;
            }
            _settingsPanel = new GameObject("SettingsPanel");
            _settingsPanel.transform.SetParent(_uiRoot.transform, false);
            _settingsPanel.AddComponent<RectTransform>();
            _settingsPanel.SetActive(false);
            
            // Create background
            var bgObj = new GameObject("SettingsBackground");
            bgObj.transform.SetParent(_settingsPanel.transform, false);
            var bgImage = bgObj.AddComponent<UIImage>();
            bgImage.color = _lcBackgroundColor;
            
            var bgRect = bgImage.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = new Vector2(400, 500);
            
            // Create gain slider with proper components
            CreateSlider("Gain", new Vector2(0, 180), 0f, 5f, LethalMicStatic.GetMicrophoneGain(), 
                (value) => {
                    LethalMicStatic.SetMicrophoneGain(value);
                    if (_config != null) _config.Bind("Audio", "Gain", 1.0f, "Microphone gain").Value = value;
                    _logger.LogInfo($"[UI] Gain changed: {value:F2}");
                }, out _gainSlider);
            
            // Create threshold slider with proper components
            CreateSlider("Threshold", new Vector2(0, 120), 0f, 1f, LethalMicStatic.GetNoiseGateThreshold(), 
                (value) => {
                    LethalMicStatic.SetNoiseGateThreshold(value);
                    if (_config != null) _config.Bind("Audio", "Threshold", 0.1f, "Voice activation threshold").Value = value;
                    _logger.LogInfo($"[UI] Threshold changed: {value:F2}");
                }, out _thresholdSlider);
            
            // Create noise gate toggle with proper components
            CreateToggle("Noise Gate", new Vector2(0, 60), LethalMicStatic.GetNoiseGateEnabled(), 
                (value) => {
                    LethalMicStatic.SetNoiseGateEnabled(value);
                    if (_config != null) _config.Bind("Audio", "NoiseGate", true, "Enable noise gate").Value = value;
                    _logger.LogInfo($"[UI] Noise Gate changed: {value}");
                }, out _noiseGateToggle);
            
            // Calibrate button
            CreateButton("Calibrate Microphone", new Vector2(0, 0), new Vector2(200, 40), OnCalibrateClicked, out _calibrateButton);
            // Close button (top right, isCloseButton = true)
            CreateButton("×", new Vector2(-20, -20), new Vector2(40, 40), OnCloseSettings, out _closeButton, true);
            
            // Validate UI components
            if (_gainSlider == null) _logger.LogError("[UI] GainSlider is null after creation!");
            if (_thresholdSlider == null) _logger.LogError("[UI] ThresholdSlider is null after creation!");
            if (_noiseGateToggle == null) _logger.LogError("[UI] NoiseGateToggle is null after creation!");
            if (_calibrateButton == null) _logger.LogError("[UI] CalibrateButton is null after creation!");
            if (_closeButton == null) _logger.LogError("[UI] CloseButton is null after creation!");
            
            _logger.LogInfo("[UI] Settings panel created with interactive controls");
        }
        
        private void CreateSlider(string labelText, Vector2 position, float minValue, float maxValue, float initialValue, 
            UnityEngine.Events.UnityAction<float> onValueChanged, out UISlider slider)
        {
            if (_settingsPanel == null)
            {
                _logger.LogError($"[UI] CreateSlider called but _settingsPanel is null for slider: {labelText}");
                slider = null;
                return;
            }
            
            // Create container
            var container = new GameObject($"{labelText}Container");
            container.transform.SetParent(_settingsPanel.transform, false);
            var containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = position;
            containerRect.sizeDelta = new Vector2(350, 50);
            
            // Create label
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
            
            // Create slider
            var sliderObj = new GameObject($"{labelText}Slider");
            sliderObj.transform.SetParent(container.transform, false);
            slider = sliderObj.AddComponent<UISlider>();
            
            var sliderRect = slider.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.45f, 0.2f);
            sliderRect.anchorMax = new Vector2(0.85f, 0.8f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;
            
            // Create background
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            var bgImage = bgObj.AddComponent<UIImage>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            var bgRect = bgImage.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            
            // Create fill area
            var fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(sliderObj.transform, false);
            var fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;
            
            // Create fill
            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);
            var fillImage = fillObj.AddComponent<UIImage>();
            fillImage.color = _lcAccentColor;
            
            var fillRect = fillImage.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            
            // Create handle slide area
            var handleSlideAreaObj = new GameObject("Handle Slide Area");
            handleSlideAreaObj.transform.SetParent(sliderObj.transform, false);
            var handleSlideAreaRect = handleSlideAreaObj.AddComponent<RectTransform>();
            handleSlideAreaRect.anchorMin = Vector2.zero;
            handleSlideAreaRect.anchorMax = Vector2.one;
            handleSlideAreaRect.offsetMin = Vector2.zero;
            handleSlideAreaRect.offsetMax = Vector2.zero;
            
            // Create handle
            var handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(handleSlideAreaObj.transform, false);
            var handleImage = handleObj.AddComponent<UIImage>();
            handleImage.color = Color.white;
            
            var handleRect = handleImage.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0, 0);
            handleRect.anchorMax = new Vector2(0, 1);
            handleRect.offsetMin = new Vector2(-10, 0);
            handleRect.offsetMax = new Vector2(10, 0);
            
            // Configure slider
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.value = initialValue;
            slider.onValueChanged.AddListener(onValueChanged);
            
            // Create value display
            var valueObj = new GameObject($"{labelText}Value");
            valueObj.transform.SetParent(container.transform, false);
            var valueText = valueObj.AddComponent<TextMeshProUGUI>();
            valueText.text = initialValue.ToString("F2");
            valueText.fontSize = 16;
            valueText.color = _lcTextColor;
            valueText.alignment = TextAlignmentOptions.Center;
            
            var valueRect = valueText.GetComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(0.9f, 0.5f);
            valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;
            
            // Update value display when slider changes
            slider.onValueChanged.AddListener((value) => valueText.text = value.ToString("F2"));
        }
        
        private void CreateToggle(string labelText, Vector2 position, bool initialValue,
            UnityEngine.Events.UnityAction<bool> onValueChanged, out UIToggle toggle)
        {
            if (_settingsPanel == null)
            {
                _logger.LogError($"[UI] CreateToggle called but _settingsPanel is null for toggle: {labelText}");
                toggle = null;
                return;
            }
            
            // Create container
            var container = new GameObject($"{labelText}Container");
            container.transform.SetParent(_settingsPanel.transform, false);
            var containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = position;
            containerRect.sizeDelta = new Vector2(350, 40);
            
            // Create toggle
            var toggleObj = new GameObject($"{labelText}Toggle");
            toggleObj.transform.SetParent(container.transform, false);
            toggle = toggleObj.AddComponent<UIToggle>();
            
            var toggleRect = toggle.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0, 0);
            toggleRect.anchorMax = new Vector2(1, 1);
            toggleRect.offsetMin = Vector2.zero;
            toggleRect.offsetMax = Vector2.zero;
            
            // Create background
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(toggleObj.transform, false);
            var bgImage = bgObj.AddComponent<UIImage>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            var bgRect = bgImage.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(0.15f, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            
            // Create checkmark
            var checkmarkObj = new GameObject("Checkmark");
            checkmarkObj.transform.SetParent(bgObj.transform, false);
            var checkmarkImage = checkmarkObj.AddComponent<UIImage>();
            checkmarkImage.color = _lcAccentColor;
            
            var checkmarkRect = checkmarkImage.GetComponent<RectTransform>();
            checkmarkRect.anchorMin = Vector2.zero;
            checkmarkRect.anchorMax = Vector2.one;
            checkmarkRect.offsetMin = Vector2.zero;
            checkmarkRect.offsetMax = Vector2.zero;
            
            // Create label
            var labelObj = new GameObject($"{labelText}Label");
            labelObj.transform.SetParent(toggleObj.transform, false);
            var labelText_comp = labelObj.AddComponent<TextMeshProUGUI>();
            labelText_comp.text = labelText;
            labelText_comp.fontSize = 18;
            labelText_comp.color = _lcTextColor;
            labelText_comp.alignment = TextAlignmentOptions.Left;
            
            var labelRect = labelText_comp.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.2f, 0);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            
            // Configure toggle
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkmarkImage;
            toggle.isOn = initialValue;
            toggle.onValueChanged.AddListener(onValueChanged);
        }
        
        private void CreateButton(string buttonText, Vector2 position, Vector2 size,
            UnityEngine.Events.UnityAction onClick, out UIButton button, bool isCloseButton = false)
        {
            if (_settingsPanel == null)
            {
                _logger.LogError($"[UI] CreateButton called but _settingsPanel is null for button: {buttonText}");
                button = null;
                return;
            }
            var buttonObj = new GameObject($"{buttonText}Button");
            buttonObj.transform.SetParent(_settingsPanel.transform, false);
            // Ensure RectTransform exists
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
            // Create background
            var bgImage = buttonObj.AddComponent<UIImage>();
            if (bgImage != null)
            bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            // Create text
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
            var colors = button.colors;
            colors.normalColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            colors.pressedColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            button.colors = colors;
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
            _logger.LogInfo($"[UI] GainSlider changed: value={value:F2}");
            if (_config == null) return;
            _config.Bind("Audio", "Gain", 1.0f, "Microphone gain").Value = value;
        }
        
        private void OnThresholdChanged(float value)
        {
            _logger.LogInfo($"[UI] ThresholdSlider changed: value={value:F2}");
            if (_config == null) return;
            _config.Bind("Audio", "Threshold", 0.1f, "Voice activation threshold").Value = value;
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
        
        private void OnCloseSettings()
        {
            if (_settingsPanel != null)
                _settingsPanel.SetActive(false);
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
        
        // Throttling variables for UI logging
        private float _lastLoggedNormalizedLevel = -1f;
        private float _lastLoggedNormalizedPeak = -1f;
        private float _lastLoggedCpuUsage = -1f;
        
        private void UpdateLevelMeter()
        {
            if (_levelMeterFill != null)
            {
                float normalizedLevel = Mathf.Clamp01(_currentMicLevel / 0.1f);
                normalizedLevel = Mathf.Sqrt(normalizedLevel);
                
                // Only log significant changes (> 5% difference)
                if (Mathf.Abs(normalizedLevel - _lastLoggedNormalizedLevel) > 0.05f)
                {
                    _logger.LogInfo($"[UI] Level meter updated: {normalizedLevel:F3} (from mic level {_currentMicLevel:F4})");
                    _lastLoggedNormalizedLevel = normalizedLevel;
                }
                
                _levelMeterFill.rectTransform.anchorMax = new Vector2(normalizedLevel, 1);
            }
        }
        
        private void UpdatePeakIndicator()
        {
            if (_peakMeterIndicator != null)
            {
                float normalizedPeak = Mathf.Clamp01(_peakMicLevel);
                
                // Only log significant changes (> 5% difference)
                if (Mathf.Abs(normalizedPeak - _lastLoggedNormalizedPeak) > 0.05f)
                {
                    _logger.LogInfo($"[UI] Peak indicator updated: {normalizedPeak:F3} (from peak level {_peakMicLevel:F4})");
                    _lastLoggedNormalizedPeak = normalizedPeak;
                }
                
                _peakMeterIndicator.rectTransform.anchorMin = new Vector2(normalizedPeak, 0);
                _peakMeterIndicator.rectTransform.anchorMax = new Vector2(normalizedPeak, 1);
            }
        }
        
        private void UpdateTextDisplays()
        {
            if (_micLevelText != null)
            {
                float dbLevel = 20 * Mathf.Log10(Mathf.Max(_currentMicLevel, 0.0001f));
                    _logger.LogInfo($"[UI] Level text updated: {dbLevel:F1} dB (from mic level {_currentMicLevel:F4})");
                _micLevelText.text = $"Level: {dbLevel:F1} dB";
            }
            
            if (_noiseFloorText != null)
            {
                float dbNoiseFloor = 20 * Mathf.Log10(Mathf.Max(_noiseFloor, 0.0001f));
                _noiseFloorText.text = $"Noise Floor: {dbNoiseFloor:F1} dB";
            }
            
            if (_cpuUsageText != null)
            {
                // Only log significant changes (> 5% difference)
                if (Mathf.Abs(_cpuUsage - _lastLoggedCpuUsage) > 5f)
                {
                    _logger.LogInfo($"[UI] CPU usage updated: {_cpuUsage:F1}%");
                    _lastLoggedCpuUsage = _cpuUsage;
                }
                
                _cpuUsageText.text = $"CPU: {_cpuUsage:F1}%";
            }
        }
        
        public void UpdateMicStatus(string status, float level)
        {
            if (!_isInitialized) return;
            float dbLevel = 20 * Mathf.Log10(Mathf.Max(level, 0.0001f));
            if (_lastLoggedStatus != status || Mathf.Abs(dbLevel - _lastLoggedMicLevel) > 1f)
            {
                _logger.LogInfo($"[UI] UpdateMicStatus: status={status}, level={level:F6}, db={dbLevel:F2}");
                _lastLoggedStatus = status;
                _lastLoggedMicLevel = dbLevel;
            }
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
            if (!_isVisible && _settingsPanel != null)
            {
                _settingsPanel.SetActive(false);
            }
        }
        
        private void OnDestroy()
        {
            if (_uiRoot != null)
            {
                Destroy(_uiRoot);
            }
        }
    }
}