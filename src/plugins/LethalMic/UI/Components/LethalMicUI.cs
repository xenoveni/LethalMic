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
        private UIToggle _noiseGateToggle;
        private UIButton _calibrateButton;
        private UIImage _levelMeterBackground;
        private UIImage _levelMeterFill;
        private UIImage _peakIndicator;
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
        private TextMeshProUGUI _calibratingText;
        
        // Add a field to store the device name
        private string _currentDeviceName = "";
        
        private float _voiceThreshold = 0.1f; // Default threshold
        private RectTransform _thresholdHandleRect;
        
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
            backgroundRect.sizeDelta = new Vector2(0, 0); // Width and height dynamic

            // Add layout group and fitter
            var layout = backgroundObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 25f; // Even spacing for all children
            layout.padding = new RectOffset(20, 20, 20, 20);

            var fitter = backgroundObj.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

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

            // Level meter fill (green bar)
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
            var peakIndicatorObj = new GameObject("PeakIndicator");
            peakIndicatorObj.transform.SetParent(meterBgObj.transform, false);
            _peakIndicator = peakIndicatorObj.AddComponent<UnityEngine.UI.Image>();
            _peakIndicator.color = Color.yellow;
            var peakIndicatorRect = _peakIndicator.GetComponent<RectTransform>();
            peakIndicatorRect.sizeDelta = new Vector2(2, 24);
            peakIndicatorRect.anchorMin = new Vector2(0, 0);
            peakIndicatorRect.anchorMax = new Vector2(0, 1);
            peakIndicatorRect.pivot = new Vector2(0.5f, 0.5f);
            peakIndicatorRect.anchoredPosition = Vector2.zero;

            // Threshold handle (draggable)
            var thresholdHandleObj = new GameObject("ThresholdHandle");
            thresholdHandleObj.transform.SetParent(meterBgObj.transform, false);
            var thresholdHandleImage = thresholdHandleObj.AddComponent<UnityEngine.UI.Image>();
            thresholdHandleImage.color = Color.yellow;
            _thresholdHandleRect = thresholdHandleObj.GetComponent<RectTransform>();
            _thresholdHandleRect.sizeDelta = new Vector2(6, 28);
            _thresholdHandleRect.anchorMin = new Vector2(_voiceThreshold, 0);
            _thresholdHandleRect.anchorMax = new Vector2(_voiceThreshold, 1);
            _thresholdHandleRect.pivot = new Vector2(0.5f, 0.5f);
            _thresholdHandleRect.anchoredPosition = Vector2.zero;

            // Add drag events to the threshold handle
            var eventTrigger = thresholdHandleObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var entryBegin = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
            entryBegin.callback.AddListener((data) => { });
            eventTrigger.triggers.Add(entryBegin);
            var entryEnd = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            entryEnd.callback.AddListener((data) => { });
            eventTrigger.triggers.Add(entryEnd);
            var entryDrag = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.Drag };
            entryDrag.callback.AddListener((data) => {
                var pointerData = (UnityEngine.EventSystems.PointerEventData)data;
                var localPos = Vector2.zero;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(meterBgRect, pointerData.position, pointerData.pressEventCamera, out localPos);
                float normalized = Mathf.Clamp01((localPos.x + meterBgRect.rect.width / 2) / meterBgRect.rect.width);
                _voiceThreshold = normalized;
                UpdateThresholdHandle();
                // Save threshold to config if needed
                if (_config != null) _config.Bind("Audio", "Threshold", _voiceThreshold, "Voice activation threshold").Value = _voiceThreshold;
            });
            eventTrigger.triggers.Add(entryDrag);

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

            // Add extra space and Calibrating... text below the button
            var calibratingContainer = new GameObject("CalibratingContainer");
            calibratingContainer.transform.SetParent(parent, false);
            var calibratingLayout = calibratingContainer.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            calibratingLayout.childAlignment = TextAnchor.MiddleCenter;
            calibratingLayout.spacing = 0;
            calibratingLayout.padding = new RectOffset(0, 0, 8, 0); // Add space above
            var calibratingFitter = calibratingContainer.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            calibratingFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            calibratingFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var calibratingTextObj = new GameObject("CalibratingText");
            calibratingTextObj.transform.SetParent(calibratingContainer.transform, false);
            _calibratingText = calibratingTextObj.AddComponent<TextMeshProUGUI>();
            _calibratingText.text = "Calibrating...";
            _calibratingText.fontSize = 18;
            _calibratingText.color = _lcAccentColor;
            _calibratingText.alignment = TextAlignmentOptions.Center;
            _calibratingText.gameObject.SetActive(false);
        }
        
        private void CreateSlider(string labelText, float minValue, float maxValue, float initialValue, 
            UnityEngine.Events.UnityAction<float> onValueChanged, out UISlider slider, Transform parent)
        {
            var container = new GameObject($"{labelText}Container");
            container.transform.SetParent(parent, false);
            var containerRect = container.AddComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(0, 40);
            var layoutElem = container.AddComponent<UnityEngine.UI.LayoutElement>();
            layoutElem.minWidth = 650;

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
            var valueLayoutElem = valueObj.AddComponent<UnityEngine.UI.LayoutElement>();
            valueLayoutElem.minWidth = 120;
            valueLayoutElem.preferredWidth = 120;
            slider.onValueChanged.AddListener((value) => valueText.text = value.ToString("F2"));
        }
        
        private void CreateToggle(string labelText, bool initialValue,
            UnityEngine.Events.UnityAction<bool> onValueChanged, out UIToggle toggle, Transform parent)
        {
            var container = new GameObject($"{labelText}Container");
            container.transform.SetParent(parent, false);
            var containerRect = container.AddComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(0, 40);
            var layoutElem = container.AddComponent<UnityEngine.UI.LayoutElement>();
            layoutElem.minWidth = 540;

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
            // Create a container for the button so it can size to content
            var buttonContainer = new GameObject($"{buttonText}ButtonContainer");
            buttonContainer.transform.SetParent(parent, false);
            var containerRect = buttonContainer.AddComponent<RectTransform>();
            var containerLayout = buttonContainer.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            containerLayout.childAlignment = TextAnchor.MiddleCenter;
            containerLayout.childForceExpandWidth = false;
            containerLayout.childForceExpandHeight = false;
            containerLayout.padding = new RectOffset(0, 0, 12, 0); // Add top margin for spacing
            var containerFitter = buttonContainer.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            containerFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            containerFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var buttonObj = new GameObject($"{buttonText}Button");
            buttonObj.transform.SetParent(buttonContainer.transform, false);
            var buttonRect = buttonObj.AddComponent<RectTransform>();
            button = buttonObj.AddComponent<UIButton>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = Vector2.zero; // Let content size it
            var buttonLayoutElem = buttonObj.AddComponent<UnityEngine.UI.LayoutElement>();
            buttonLayoutElem.minWidth = 110; // Ensure button is wide enough for text
            buttonLayoutElem.preferredWidth = 120;
            buttonLayoutElem.minHeight = 32;
            buttonLayoutElem.preferredHeight = 36;

            // Border for button (visible outline)
            var borderObj = new GameObject("ButtonBorder");
            borderObj.transform.SetParent(buttonObj.transform, false);
            var borderImage = borderObj.AddComponent<UnityEngine.UI.Image>();
            borderImage.color = new Color(0.3f, 0.3f, 0.3f, 1f); // Use dark gray border for visibility
            var borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2, -2); // 2px padding for border
            borderRect.offsetMax = new Vector2(2, 2);
            borderImage.raycastTarget = false;
            borderImage.type = UnityEngine.UI.Image.Type.Sliced;
            borderImage.pixelsPerUnitMultiplier = 1f;
            // Button background (solid black)
            var bgImage = buttonObj.AddComponent<UnityEngine.UI.Image>();
            if (bgImage != null)
            {
                bgImage.color = Color.black;
                bgImage.raycastTarget = true; // Ensure button is clickable
            }
            button.targetGraphic = bgImage; // Set background as target graphic

            // Add 3D press effect using Button ColorBlock
            var colors = button.colors;
            colors.normalColor = Color.black;
            colors.highlightedColor = new Color(0.12f, 0.12f, 0.12f, 1f); // Slightly lighter black
            colors.pressedColor = new Color(0.18f, 0.18f, 0.18f, 1f); // Slightly lighter black for pressed
            colors.selectedColor = Color.black;
            colors.disabledColor = new Color(0.08f, 0.08f, 0.08f, 1f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            // Remove Press3DEffect if present
            var press3D = buttonObj.GetComponent<LethalMic.UI.Components.Press3DEffect>();
            if (press3D != null) Destroy(press3D);

            // Text with padding
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            var textComp = textObj.AddComponent<TextMeshProUGUI>();
            if (textComp != null)
            {
                textComp.text = buttonText;
                textComp.fontSize = isCloseButton ? 24 : 16;
                textComp.color = Color.white; // Force white text for readability
                textComp.alignment = TextAlignmentOptions.Center;
                var textRect = textComp.GetComponent<RectTransform>();
                if (textRect != null)
                {
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.offsetMin = new Vector2(8, 2); // Add horizontal and vertical padding
                    textRect.offsetMax = new Vector2(-8, -2);
                }
            }
            // Ensure border is behind everything else
            borderObj.transform.SetSiblingIndex(0);
            button.onClick.AddListener(onClick);
        }
        
        private void LoadSettings()
        {
            if (_config == null) return;
            
            var gain = _config.Bind("Audio", "Gain", 1.0f, "Microphone gain").Value;
            
            if (_gainSlider != null) _gainSlider.value = gain;
        }
        
        private void OnGainChanged(float value)
        {
            LethalMic.LethalMicStatic.SetMicrophoneGain(value);
            LogThrottled($"[UI] GainSlider changed: value={value:F2}");
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
            if (_calibratingText != null) _calibratingText.gameObject.SetActive(true); // Show text
            StartCoroutine(CalibrateNoiseFloorCoroutine());
        }
        
        private IEnumerator CalibrateNoiseFloorCoroutine()
        {
            _isCalibrating = true;
            _logger.LogInfo("Starting noise floor calibration...");
            if (_calibratingText != null) _calibratingText.gameObject.SetActive(true);
            yield return new WaitForSeconds(3f);
            _logger.LogInfo("Noise floor calibration complete");
            _isCalibrating = false;
            if (_calibratingText != null) _calibratingText.gameObject.SetActive(false); // Hide text
        }
        
        private void Update()
        {
            if (!_isVisible || !_isInitialized) return;
            
            // Update level meter
            UpdateLevelMeter();
            
            // Update text displays
            UpdateTextDisplays();
        }
        
        private void UpdateLevelMeter()
        {
            if (_levelMeterFill != null)
            {
                // Convert linear level to dB for better visualization
                float dbLevel = 20 * Mathf.Log10(Mathf.Max(_currentMicLevel, 0.0001f));
                float normalizedLevel = Mathf.Clamp01((dbLevel + 60f) / 60f); // Map -60dB to 0dB to 0-1 range
                
                // Update fill bar
                _levelMeterFill.rectTransform.anchorMin = new Vector2(0, 0);
                _levelMeterFill.rectTransform.anchorMax = new Vector2(normalizedLevel, 1);
                
                // Color coding based on level
                if (normalizedLevel > 0.9f) // Above -6dB
                {
                    _levelMeterFill.color = Color.red; // Warning color for high levels
                }
                else if (normalizedLevel > _voiceThreshold)
                {
                    _levelMeterFill.color = Color.green; // Active voice
                }
                else
                {
                    _levelMeterFill.color = _lcAccentColor; // Normal level
                }
                
                // Add peak indicator
                float peakDb = 20 * Mathf.Log10(Mathf.Max(_peakMicLevel, 0.0001f));
                float normalizedPeak = Mathf.Clamp01((peakDb + 60f) / 60f);
                if (_peakIndicator != null)
                {
                    _peakIndicator.rectTransform.anchorMin = new Vector2(normalizedPeak - 0.01f, 0);
                    _peakIndicator.rectTransform.anchorMax = new Vector2(normalizedPeak + 0.01f, 1);
                }
            }
            
            if (_thresholdHandleRect != null)
            {
                // Convert threshold to dB scale
                float thresholdDb = 20 * Mathf.Log10(Mathf.Max(_voiceThreshold, 0.0001f));
                float normalizedThreshold = Mathf.Clamp01((thresholdDb + 60f) / 60f);
                
                _thresholdHandleRect.anchorMin = new Vector2(normalizedThreshold, 0);
                _thresholdHandleRect.anchorMax = new Vector2(normalizedThreshold, 1);
            }
            
            if (_noInputWarningText != null)
            {
                _noInputWarningText.gameObject.SetActive(_currentMicLevel < 0.001f);
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
        
        public void UpdateMicStatus(string deviceName, string status, float level)
        {
            if (!_isInitialized) return;
            Debug.Log($"[LethalMicUI] UpdateMicStatus received level: {level}");
            float dbLevel = 20 * Mathf.Log10(Mathf.Max(level, 0.0001f));
            _currentMicLevel = level;
            _micLevels[_micLevelIndex] = level;
            _micLevelIndex = (_micLevelIndex + 1) % _micLevels.Length;
            _peakMicLevel = Mathf.Max(_peakMicLevel * 0.95f, level);
            _currentDeviceName = deviceName;
            if (_micStatusText != null)
            {
                // Show device name and status, matching the game's style
                _micStatusText.text = $"Current input device: {_currentDeviceName}\nStatus: {status}";
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

        private void UpdateThresholdHandle()
        {
            if (_thresholdHandleRect != null)
            {
                _thresholdHandleRect.anchorMin = new Vector2(_voiceThreshold, 0);
                _thresholdHandleRect.anchorMax = new Vector2(_voiceThreshold, 1);
            }
        }
    }
}