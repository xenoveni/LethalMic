using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace LethalMic
{
    /// <summary>
    /// In-game UI system for LethalMic settings and microphone visualization
    /// </summary>
    public class LethalMicUI : MonoBehaviour
    {
        private LethalMic _plugin;
        private bool _showUI = false;
        private static readonly string LogPrefix = "[LethalMicUI]";
        private Rect _windowRect = new Rect(50, 50, 600, 700);
        private Vector2 _scrollPosition = Vector2.zero;
        private GUIStyle _windowStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _sliderStyle;
        private GUIStyle _toggleStyle;
        private bool _stylesInitialized = false;
        
        // Microphone visualization
        private float[] _micLevels = new float[100]; // Rolling buffer for mic levels
        private int _micLevelIndex = 0;
        private float _currentMicLevel = 0f;
        private float _peakMicLevel = 0f;
        private float _noiseFloor = 0f;
        private bool _voiceDetected = false;
        private float _vadThreshold = -30f;
        
        // Settings tabs
        private enum SettingsTab
        {
            Microphone,
            Processing,
            Advanced,
            Presets,
            Performance
        }
        private SettingsTab _currentTab = SettingsTab.Microphone;
        
        // Preset management
        private string _newPresetName = "";
        private List<string> _availablePresets = new List<string>();
        
        // Performance monitoring
        private float _cpuUsage = 0f;
        private int _processedSamples = 0;
        private float _lastUpdateTime = 0f;
        
        private void Awake()
        {
            Debug.Log($"{LogPrefix} Awake() called - Component created");
            Debug.Log($"{LogPrefix} GameObject: {gameObject.name}");
            Debug.Log($"{LogPrefix} Instance ID: {GetInstanceID()}");
            Debug.Log($"{LogPrefix} Component enabled: {enabled}");
        }
        
        private void OnEnable()
        {
            Debug.Log($"{LogPrefix} OnEnable() called - Component enabled");
        }
        
        private void Start()
        {
            Debug.Log($"{LogPrefix} Start() called - Component started");
            Debug.Log($"{LogPrefix} Plugin reference: {_plugin != null}");
        }
        
        private void OnDisable()
        {
            Debug.Log($"{LogPrefix} OnDisable() called - Component disabled");
        }
        
        private void OnDestroy()
        {
            Debug.Log($"{LogPrefix} OnDestroy() called - Component destroyed");
        }
        
        public void Initialize(LethalMic plugin)
        {
            Debug.Log($"{LogPrefix} Initialize() called");
            _plugin = plugin;
            Debug.Log($"{LogPrefix} Plugin reference set: {_plugin != null}");
            
            _windowRect = new Rect(Screen.width - 650, 50, 600, 700);
            Debug.Log($"{LogPrefix} Window rect set: {_windowRect}");
            Debug.Log($"{LogPrefix} Screen size: {Screen.width}x{Screen.height}");
            
            // Initialize microphone level buffer
            for (int i = 0; i < _micLevels.Length; i++)
            {
                _micLevels[i] = -60f; // Start with silence
            }
            Debug.Log($"{LogPrefix} Microphone level buffer initialized with {_micLevels.Length} elements");
            
            RefreshPresetList();
            Debug.Log($"{LogPrefix} Initialize() completed successfully");
        }
        
        private void Update()
        {
            // Log Update calls every 5 seconds to avoid spam
            if (Time.time % 5f < Time.deltaTime)
            {
                Debug.Log($"{LogPrefix} Update() running - UI visible: {_showUI}, Plugin: {_plugin != null}");
            }
            
            // Toggle UI with F8 key
            if (Input.GetKeyDown(KeyCode.F8))
            {
                Debug.Log($"{LogPrefix} F8 key pressed! Current UI state: {_showUI}");
                _showUI = !_showUI;
                Debug.Log($"{LogPrefix} UI state changed to: {_showUI}");
                
                if (_showUI)
                {
                    Debug.Log($"{LogPrefix} UI opened - refreshing preset list");
                    RefreshPresetList();
                }
                else
                {
                    Debug.Log($"{LogPrefix} UI closed");
                }
            }
            
            // Check for any input issues
            if (Time.time % 10f < Time.deltaTime) // Every 10 seconds
            {
                Debug.Log($"{LogPrefix} Input system check - Any key: {Input.anyKey}, Input enabled: {Input.inputString != null}");
            }
            
            // Update microphone visualization data
            UpdateMicrophoneVisualization();
            
            // Update performance metrics
            UpdatePerformanceMetrics();
        }
        
        private void OnGUI()
        {
            // Log OnGUI calls every 2 seconds when UI should be visible
            if (_showUI && Time.time % 2f < Time.deltaTime)
            {
                Debug.Log($"{LogPrefix} OnGUI() called - UI should be visible: {_showUI}");
                Debug.Log($"{LogPrefix} Screen size in OnGUI: {Screen.width}x{Screen.height}");
                Debug.Log($"{LogPrefix} Event type: {Event.current?.type}");
            }
            
            if (!_showUI)
            {
                // Log once when UI is hidden
                if (Time.time % 5f < Time.deltaTime)
                {
                    Debug.Log($"{LogPrefix} OnGUI() - UI hidden, returning early");
                }
                return;
            }
            
            try
            {
                InitializeStyles();
                
                // Main window
                _windowRect = GUI.Window(12345, _windowRect, DrawMainWindow, "LethalMic Settings & Monitor", _windowStyle);
                
                // Clamp window to screen
                _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
                _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - _windowRect.height);
                
                // Log successful GUI rendering occasionally
                if (Time.time % 3f < Time.deltaTime)
                {
                    Debug.Log($"{LogPrefix} GUI rendered successfully at position: {_windowRect.position}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} Error in OnGUI: {ex.Message}");
                Debug.LogError($"{LogPrefix} Stack trace: {ex.StackTrace}");
            }
        }
        
        private void InitializeStyles()
        {
            if (_stylesInitialized) return;
            
            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true
            };
            
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            
            _sliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
            _toggleStyle = new GUIStyle(GUI.skin.toggle)
            {
                fontSize = 12
            };
            
            _stylesInitialized = true;
        }
        
        private void DrawMainWindow(int windowID)
        {
            GUILayout.BeginVertical();
            
            // Header with close button
            GUILayout.BeginHorizontal();
            GUILayout.Label("LethalMic Audio Control Panel", _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("×", GUILayout.Width(25), GUILayout.Height(25)))
            {
                _showUI = false;
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Tab buttons
            DrawTabButtons();
            
            GUILayout.Space(10);
            
            // Scrollable content area
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(500));
            
            switch (_currentTab)
            {
                case SettingsTab.Microphone:
                    DrawMicrophoneTab();
                    break;
                case SettingsTab.Processing:
                    DrawProcessingTab();
                    break;
                case SettingsTab.Advanced:
                    DrawAdvancedTab();
                    break;
                case SettingsTab.Presets:
                    DrawPresetsTab();
                    break;
                case SettingsTab.Performance:
                    DrawPerformanceTab();
                    break;
            }
            
            GUILayout.EndScrollView();
            
            // Footer with hotkey info
            GUILayout.Space(10);
            GUILayout.Label("Press F8 to toggle this window", _labelStyle);
            
            GUILayout.EndVertical();
            
            GUI.DragWindow();
        }
        
        private void DrawTabButtons()
        {
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Toggle(_currentTab == SettingsTab.Microphone, "Microphone", _buttonStyle))
                _currentTab = SettingsTab.Microphone;
            if (GUILayout.Toggle(_currentTab == SettingsTab.Processing, "Processing", _buttonStyle))
                _currentTab = SettingsTab.Processing;
            if (GUILayout.Toggle(_currentTab == SettingsTab.Advanced, "Advanced", _buttonStyle))
                _currentTab = SettingsTab.Advanced;
            if (GUILayout.Toggle(_currentTab == SettingsTab.Presets, "Presets", _buttonStyle))
                _currentTab = SettingsTab.Presets;
            if (GUILayout.Toggle(_currentTab == SettingsTab.Performance, "Performance", _buttonStyle))
                _currentTab = SettingsTab.Performance;
            
            GUILayout.EndHorizontal();
        }
        
        private void DrawMicrophoneTab()
        {
            GUILayout.Label("Microphone Activity Monitor", _headerStyle);
            GUILayout.Space(10);
            
            // Current microphone level
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Current Level: {_currentMicLevel:F1} dB", _labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Peak: {_peakMicLevel:F1} dB", _labelStyle);
            GUILayout.EndHorizontal();
            
            // Voice activity indicator
            GUILayout.BeginHorizontal();
            GUILayout.Label("Voice Detected:", _labelStyle);
            GUI.color = _voiceDetected ? Color.green : Color.red;
            GUILayout.Label(_voiceDetected ? "YES" : "NO", _labelStyle);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Microphone level visualization
            DrawMicrophoneLevelGraph();
            
            GUILayout.Space(20);
            
            // VAD Threshold control
            GUILayout.Label($"Voice Detection Threshold: {_vadThreshold:F1} dB", _labelStyle);
            _vadThreshold = GUILayout.HorizontalSlider(_vadThreshold, -60f, 0f);
            
            GUILayout.Space(10);
            
            // Noise floor indicator
            GUILayout.Label($"Noise Floor: {_noiseFloor:F1} dB", _labelStyle);
            
            GUILayout.Space(10);
            
            // Quick actions
            GUILayout.Label("Quick Actions:", _headerStyle);
            if (GUILayout.Button("Reset Peak Level", _buttonStyle))
            {
                _peakMicLevel = _currentMicLevel;
            }
            
            if (GUILayout.Button("Calibrate Noise Floor", _buttonStyle))
            {
                CalibrateNoiseFloor();
            }
        }
        
        private void DrawMicrophoneLevelGraph()
        {
            Rect graphRect = GUILayoutUtility.GetRect(550, 100);
            
            // Background
            GUI.color = Color.black;
            GUI.DrawTexture(graphRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            
            // Grid lines
            GUI.color = Color.gray;
            for (int i = 1; i < 4; i++)
            {
                float y = graphRect.y + (graphRect.height / 4) * i;
                GUI.DrawTexture(new Rect(graphRect.x, y, graphRect.width, 1), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            
            // Draw microphone levels
            GUI.color = Color.green;
            for (int i = 1; i < _micLevels.Length; i++)
            {
                float x1 = graphRect.x + (graphRect.width / _micLevels.Length) * (i - 1);
                float x2 = graphRect.x + (graphRect.width / _micLevels.Length) * i;
                
                float y1 = graphRect.y + graphRect.height - (((_micLevels[i - 1] + 60f) / 60f) * graphRect.height);
                float y2 = graphRect.y + graphRect.height - (((_micLevels[i] + 60f) / 60f) * graphRect.height);
                
                y1 = Mathf.Clamp(y1, graphRect.y, graphRect.y + graphRect.height);
                y2 = Mathf.Clamp(y2, graphRect.y, graphRect.y + graphRect.height);
                
                DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), Color.green);
            }
            
            // Draw VAD threshold line
            GUI.color = Color.red;
            float thresholdY = graphRect.y + graphRect.height - (((_vadThreshold + 60f) / 60f) * graphRect.height);
            GUI.DrawTexture(new Rect(graphRect.x, thresholdY, graphRect.width, 2), Texture2D.whiteTexture);
            
            GUI.color = Color.white;
            
            // Labels
            GUI.Label(new Rect(graphRect.x, graphRect.y - 20, 100, 20), "0 dB");
            GUI.Label(new Rect(graphRect.x, graphRect.y + graphRect.height, 100, 20), "-60 dB");
        }
        
        private void DrawLine(Vector2 start, Vector2 end, Color color)
        {
            GUI.color = color;
            Vector2 direction = (end - start).normalized;
            float distance = Vector2.Distance(start, end);
            
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y, distance, 2), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = Color.white;
        }
        
        private void DrawProcessingTab()
        {
            GUILayout.Label("Audio Processing Settings", _headerStyle);
            GUILayout.Space(10);
            
            // Get config values through reflection or direct access
            DrawConfigToggle("Enable Audio Processing", "enableAudioProcessing");
            DrawConfigToggle("Noise Suppression", "enableNoiseSuppression");
            DrawConfigToggle("Automatic Gain Control", "enableAGC");
            DrawConfigToggle("Voice Activity Detection", "enableVAD");
            DrawConfigToggle("Echo Cancellation", "enableEchoCancellation");
            DrawConfigToggle("Loop Detection", "enableLoopDetection");
            
            GUILayout.Space(10);
            
            DrawConfigSlider("Noise Reduction Strength", "noiseReductionStrength", 0f, 1f);
            DrawConfigSlider("Echo Cancellation Strength", "echoCancellationStrength", 0f, 1f);
            DrawConfigSlider("Loop Detection Threshold", "loopDetectionThreshold", 0f, 1f);
            DrawConfigSlider("Loop Suppression Strength", "loopSuppressionStrength", 0f, 1f);
        }
        
        private void DrawAdvancedTab()
        {
            GUILayout.Label("Advanced Settings", _headerStyle);
            GUILayout.Space(10);
            
            DrawConfigToggle("AI Noise Suppression", "enableAIDenoise");
            DrawConfigToggle("Spectral Subtraction", "enableSpectralSubtraction");
            DrawConfigToggle("Adaptive Noise Floor", "enableAdaptiveNoiseFloor");
            DrawConfigToggle("Voice Ducking", "enableVoiceDucking");
            DrawConfigToggle("Frequency Domain Processing", "enableFrequencyDomainProcessing");
            
            GUILayout.Space(10);
            
            DrawConfigSlider("Noise Gate Threshold", "noiseGateThreshold", -60f, 0f);
            DrawConfigSlider("Voice Ducking Level", "voiceDuckingLevel", 0f, 1f);
            DrawConfigSlider("Adaptive Threshold Sensitivity", "adaptiveThresholdSensitivity", 0f, 1f);
            
            GUILayout.Space(10);
            
            GUILayout.Label("Processing Quality:", _labelStyle);
            string[] qualityOptions = { "Low", "Medium", "High", "Ultra" };
            int currentQuality = GetConfigInt("processingQuality");
            int newQuality = GUILayout.SelectionGrid(currentQuality, qualityOptions, 4, _buttonStyle);
            if (newQuality != currentQuality)
            {
                SetConfigInt("processingQuality", newQuality);
            }
        }
        
        private void DrawPresetsTab()
        {
            GUILayout.Label("Audio Presets", _headerStyle);
            GUILayout.Space(10);
            
            // Current preset
            string currentPreset = GetConfigString("currentPresetName");
            GUILayout.Label($"Current Preset: {currentPreset}", _labelStyle);
            
            GUILayout.Space(10);
            
            // Available presets
            GUILayout.Label("Available Presets:", _labelStyle);
            foreach (string preset in _availablePresets)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(preset, _buttonStyle))
                {
                    SetConfigString("currentPresetName", preset);
                }
                if (preset != "Default" && GUILayout.Button("Delete", _buttonStyle, GUILayout.Width(60)))
                {
                    DeletePreset(preset);
                }
                GUILayout.EndHorizontal();
            }
            
            GUILayout.Space(20);
            
            // Create new preset
            GUILayout.Label("Create New Preset:", _labelStyle);
            GUILayout.BeginHorizontal();
            _newPresetName = GUILayout.TextField(_newPresetName, GUILayout.Width(200));
            if (GUILayout.Button("Save Current Settings", _buttonStyle) && !string.IsNullOrEmpty(_newPresetName))
            {
                SaveCurrentAsPreset(_newPresetName);
                _newPresetName = "";
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Refresh Preset List", _buttonStyle))
            {
                RefreshPresetList();
            }
        }
        
        private void DrawPerformanceTab()
        {
            GUILayout.Label("Performance Monitor", _headerStyle);
            GUILayout.Space(10);
            
            GUILayout.Label($"CPU Usage: {_cpuUsage:F1}%", _labelStyle);
            GUILayout.Label($"Processed Samples: {_processedSamples:N0}", _labelStyle);
            GUILayout.Label($"Frame Rate: {(1f / Time.deltaTime):F1} FPS", _labelStyle);
            
            GUILayout.Space(20);
            
            DrawConfigToggle("Adaptive Processing", "enableAdaptiveProcessing");
            DrawConfigToggle("Artifact Reduction", "enableArtifactReduction");
            
            GUILayout.Space(10);
            
            DrawConfigSlider("Max CPU Usage", "maxCpuUsagePercent", 1f, 20f);
            
            GUILayout.Space(10);
            
            GUILayout.Label("FFT Size:", _labelStyle);
            string[] fftOptions = { "256", "512", "1024", "2048", "4096" };
            int[] fftValues = { 256, 512, 1024, 2048, 4096 };
            int currentFFT = GetConfigInt("fftSize");
            int currentIndex = Array.IndexOf(fftValues, currentFFT);
            if (currentIndex == -1) currentIndex = 2; // Default to 1024
            
            int newIndex = GUILayout.SelectionGrid(currentIndex, fftOptions, 5, _buttonStyle);
            if (newIndex != currentIndex)
            {
                SetConfigInt("fftSize", fftValues[newIndex]);
            }
        }
        
        private void DrawConfigToggle(string label, string configName)
        {
            bool currentValue = GetConfigBool(configName);
            bool newValue = GUILayout.Toggle(currentValue, label, _toggleStyle);
            if (newValue != currentValue)
            {
                SetConfigBool(configName, newValue);
            }
        }
        
        private void DrawConfigSlider(string label, string configName, float min, float max)
        {
            float currentValue = GetConfigFloat(configName);
            GUILayout.Label($"{label}: {currentValue:F2}", _labelStyle);
            float newValue = GUILayout.HorizontalSlider(currentValue, min, max, _sliderStyle, GUI.skin.horizontalSliderThumb);
            if (Math.Abs(newValue - currentValue) > 0.01f)
            {
                SetConfigFloat(configName, newValue);
            }
        }
        
        // Configuration access methods (using reflection to access private fields)
        private bool GetConfigBool(string configName)
        {
            try
            {
                var field = _plugin.GetType().GetField(configName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(_plugin) is ConfigEntry<bool> config)
                {
                    return config.Value;
                }
            }
            catch { }
            return false;
        }
        
        private void SetConfigBool(string configName, bool value)
        {
            try
            {
                var field = _plugin.GetType().GetField(configName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(_plugin) is ConfigEntry<bool> config)
                {
                    config.Value = value;
                }
            }
            catch { }
        }
        
        private float GetConfigFloat(string configName)
        {
            try
            {
                var field = _plugin.GetType().GetField(configName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(_plugin) is ConfigEntry<float> config)
                {
                    return config.Value;
                }
            }
            catch { }
            return 0f;
        }
        
        private void SetConfigFloat(string configName, float value)
        {
            try
            {
                var field = _plugin.GetType().GetField(configName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(_plugin) is ConfigEntry<float> config)
                {
                    config.Value = value;
                }
            }
            catch { }
        }
        
        private int GetConfigInt(string configName)
        {
            try
            {
                var field = _plugin.GetType().GetField(configName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(_plugin) is ConfigEntry<int> config)
                {
                    return config.Value;
                }
            }
            catch { }
            return 0;
        }
        
        private void SetConfigInt(string configName, int value)
        {
            try
            {
                var field = _plugin.GetType().GetField(configName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(_plugin) is ConfigEntry<int> config)
                {
                    config.Value = value;
                }
            }
            catch { }
        }
        
        private string GetConfigString(string configName)
        {
            try
            {
                var field = _plugin.GetType().GetField(configName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(_plugin) is ConfigEntry<string> config)
                {
                    return config.Value;
                }
            }
            catch { }
            return "";
        }
        
        private void SetConfigString(string configName, string value)
        {
            try
            {
                var field = _plugin.GetType().GetField(configName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(_plugin) is ConfigEntry<string> config)
                {
                    config.Value = value;
                }
            }
            catch { }
        }
        
        private void UpdateMicrophoneVisualization()
        {
            if (_plugin == null) return;
            
            // Get real microphone level data from the plugin
            _currentMicLevel = _plugin.GetCurrentMicrophoneLevel();
            
            if (_currentMicLevel > _peakMicLevel)
            {
                _peakMicLevel = _currentMicLevel;
            }
            
            // Update rolling buffer
            _micLevels[_micLevelIndex] = _currentMicLevel;
            _micLevelIndex = (_micLevelIndex + 1) % _micLevels.Length;
            
            // Get voice activity detection from plugin
            _voiceDetected = _plugin.IsVoiceDetected();
            
            // Get noise floor from plugin
            float pluginNoiseFloor = _plugin.GetNoiseFloor();
            if (pluginNoiseFloor != 0f)
            {
                _noiseFloor = pluginNoiseFloor;
            }
        }
        
        private void UpdatePerformanceMetrics()
        {
            if (_plugin == null) return;
            
            if (Time.time - _lastUpdateTime > 1f)
            {
                _cpuUsage = _plugin.GetCPUUsage();
                _processedSamples += 48000; // Approximate samples per second
                _lastUpdateTime = Time.time;
            }
        }
        
        private void CalibrateNoiseFloor()
        {
            _noiseFloor = _currentMicLevel;
            _plugin.Logger.LogInfo($"Noise floor calibrated to {_noiseFloor:F1} dB");
        }
        
        private void RefreshPresetList()
        {
            _availablePresets.Clear();
            _availablePresets.AddRange(new[] { "Default", "High Quality", "Low Latency", "Gaming", "Streaming" });
            
            // In a real implementation, this would scan the preset directory
            try
            {
                // Add any custom presets found
                // This is a placeholder for actual preset scanning
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogError($"Error refreshing preset list: {ex.Message}");
            }
        }
        
        private void SaveCurrentAsPreset(string presetName)
        {
            try
            {
                // In a real implementation, this would save current settings as a preset
                _plugin.Logger.LogInfo($"Saved current settings as preset: {presetName}");
                RefreshPresetList();
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogError($"Error saving preset: {ex.Message}");
            }
        }
        
        private void DeletePreset(string presetName)
        {
            try
            {
                // In a real implementation, this would delete the preset file
                _plugin.Logger.LogInfo($"Deleted preset: {presetName}");
                RefreshPresetList();
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogError($"Error deleting preset: {ex.Message}");
            }
        }
        
        // Public methods for the main plugin to update visualization data
        public void UpdateMicrophoneLevel(float level)
        {
            _currentMicLevel = level;
            if (level > _peakMicLevel)
            {
                _peakMicLevel = level;
            }
        }
        
        public void UpdateVoiceActivity(bool detected)
        {
            _voiceDetected = detected;
        }
        
        public void UpdateNoiseFloor(float noiseFloor)
        {
            _noiseFloor = noiseFloor;
        }
        
        public void UpdateCPUUsage(float usage)
        {
            _cpuUsage = usage;
        }
    }
}