using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;
using GameNetcodeStuff;
using UnityEngine.SceneManagement;
using static LethalMic.Core.PluginInfo;
using LethalMic.UI.Components;
using System.Linq;

namespace LethalMic
{
    /// <summary>
    /// Static implementation of LethalMic that doesn't rely on MonoBehaviour lifecycle
    /// This version uses static classes and Harmony patches to avoid destruction issues
    /// </summary>
    [BepInPlugin(global::LethalMic.Core.PluginInfo.PLUGIN_GUID + ".Static", global::LethalMic.Core.PluginInfo.PLUGIN_NAME + " (Static)", global::LethalMic.Core.PluginInfo.PLUGIN_VERSION)]
    public class LethalMicStatic : BaseUnityPlugin
    {
        // Static references
        private static new ManualLogSource Logger;  // Use 'new' to explicitly hide base Logger
        private static Harmony HarmonyInstance;
        private static ConfigFile ConfigFile;
        private static bool IsInitialized = false;
        private static DateTime LastLogTime = DateTime.MinValue;
        private static int LogCounter = 0;
        
        // Audio processing
        private static AudioClip microphoneClip;
        private static string selectedDevice;
        private static bool isRecording = false;
        private static float currentMicrophoneLevel = -60f;
        private static bool voiceDetected = false;
        private static float noiseFloor = -60f;
        private static float cpuUsage = 0f;
        private static int audioFrameCount = 0;
        private static int errorCount = 0;
        private static float peakMicLevel = -60f;
        
        // Configuration
        private static ConfigEntry<bool> EnableMod;
        private static ConfigEntry<float> MicrophoneGain;
        private static ConfigEntry<string> InputDevice;
        private static ConfigEntry<bool> NoiseGate;
        private static ConfigEntry<float> NoiseGateThreshold;
        private static ConfigEntry<bool> DebugLogging;
        
        // UI
        private static GameObject uiObject;
        private static LethalMicUI uiInstance;
        private static GameObject updaterObject;
        
        // Throttle logging
        private static DateTime _lastSummaryLog = DateTime.MinValue;
        
        // Override Awake from BaseUnityPlugin
        private void Awake()
        {
            try
            {
                // Initialize logger first
                Logger = BepInEx.Logging.Logger.CreateLogSource("LethalMic");
                if (Logger == null)
                {
                    UnityEngine.Debug.LogError("[LethalMic] Failed to create logger!");
                    return;
                }

                GetLogger().LogInfo("Initializing LethalMic static class...");
                
                // Initialize configuration
                ConfigFile = Config;  // Use the base class Config property
                
                // Load settings
                EnableMod = ConfigFile.Bind("General", "EnableMod", true, "Enable/disable the LethalMic mod");
                MicrophoneGain = ConfigFile.Bind("Audio", "MicrophoneGain", 1.0f, "Microphone input gain (0.0 to 5.0)");
                InputDevice = ConfigFile.Bind("Audio", "InputDevice", "", "Preferred input device (empty for default)");
                NoiseGate = ConfigFile.Bind("Audio", "NoiseGate", true, "Enable noise gate");
                NoiseGateThreshold = ConfigFile.Bind("Audio", "NoiseGateThreshold", 0.01f, "Noise gate threshold (0.0 to 1.0)");
                DebugLogging = ConfigFile.Bind("Debug", "DebugLogging", false, "Enable debug logging");
                
                GetLogger().LogInfo($"Configuration loaded - Enabled: {EnableMod.Value}, Gain: {MicrophoneGain.Value}");
                
                // Initialize audio system
                InitializeAudio();
                
                // Apply Harmony patches
                HarmonyInstance = new Harmony("com.xenoveni.lethalmic");
                GetLogger().LogInfo("Applying Harmony patches...");
                HarmonyInstance.PatchAll();
                GetLogger().LogInfo("Harmony patches applied successfully");
                
                // Initialize UI
                InitializeUI();
                
                IsInitialized = true;
                GetLogger().LogInfo("Successfully initialized with static implementation");
                GetLogger().LogInfo($"Initialization completed at {DateTime.Now}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LethalMic] Error in Awake: {ex}");
                if (Logger != null)
                {
                    Logger.LogError($"Error in Awake: {ex}");
                }
            }
        }
        
        private static string _selectedDeviceName = null;
        private static void InitializeAudio()
        {
            if (!EnableMod.Value)
            {
                GetLogger().LogInfo("Mod disabled in configuration");
                return;
            }
            
            try
            {
                GetLogger().LogInfo("Initializing audio system...");
                
                // Get available microphones
                var devices = Microphone.devices;
                GetLogger().LogInfo($"Found {devices.Length} microphone devices");
                
                foreach (var device in devices)
                {
                    GetLogger().LogInfo($"Available device: {device}");
                }
                
                // Select device
                selectedDevice = string.IsNullOrEmpty(InputDevice.Value) ? null : InputDevice.Value;
                GetLogger().LogInfo($"Selected device: {selectedDevice ?? "default"}");
                
                // Test microphone access
                if (devices.Length > 0)
                {
                    GetLogger().LogInfo("Audio system ready");
                    GetLogger().LogInfo($"Sample rate: {AudioSettings.outputSampleRate}Hz");
                    GetLogger().LogInfo($"Speaker mode: {AudioSettings.speakerMode}");
                    
                    // Update UI with microphone status
                    if (uiInstance != null)
                    {
                        uiInstance.UpdateMicStatus("Connected", 0f);
                    }
                }
                else
                {
                    GetLogger().LogWarning("No microphone devices found");
                    if (uiInstance != null)
                    {
                        uiInstance.UpdateMicStatus("Not Found", 0f);
                    }
                }

                _selectedDeviceName = devices[0]; // Use the first available device
                GetLogger().LogInfo($"[AUDIO] Using device: {_selectedDeviceName}");
                microphoneClip = Microphone.Start(_selectedDeviceName, true, 10, 44100);
                if (microphoneClip == null)
                {
                    GetLogger().LogError("[AUDIO] Failed to start microphone!");
                    return;
                }
                GetLogger().LogInfo($"[AUDIO] Microphone started. Sample rate: {microphoneClip.frequency}, Channels: {microphoneClip.channels}");
            }
            catch (Exception ex)
            {
                GetLogger().LogError($"Failed to initialize audio: {ex}");
                GetLogger().LogError($"Stack trace: {ex.StackTrace}");
                errorCount++;
                
                if (uiInstance != null)
                {
                    uiInstance.UpdateMicStatus("Error", 0f);
                }
            }
        }
        
        private static void InitializeUI()
        {
            try
            {
                GetLogger().LogInfo("Initializing UI...");
                
                // Create UI GameObject
                uiObject = new GameObject("LethalMicUI");
                uiObject.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(uiObject);
                
                // Add UI component
                uiInstance = uiObject.AddComponent<LethalMicUI>();
                uiInstance.Initialize(Logger, ConfigFile);
                
                // Ensure UI stays active
                uiObject.SetActive(true);

                // Create updater GameObject
                updaterObject = new GameObject("LethalMicUpdater");
                updaterObject.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(updaterObject);
                updaterObject.AddComponent<LethalMicUpdater>();
                
                GetLogger().LogInfo("UI initialized successfully");
            }
            catch (Exception ex)
            {
                GetLogger().LogError($"Failed to initialize UI: {ex}");
            }
        }
        
        public static void ToggleUI()
        {
            try
            {
                GetLogger()?.LogInfo("[UI] ToggleUI called - attempting to toggle UI visibility");
                if (uiInstance != null)
                {
                    GetLogger()?.LogInfo("[UI] uiInstance is not null, calling ToggleVisibility()");
                    uiInstance.ToggleVisibility();
                    GetLogger()?.LogInfo($"[UI] UI visibility is now: {uiInstance.IsVisible}");
                }
                else
                {
                    GetLogger()?.LogWarning("[UI] ToggleUI called but UI instance is null");
                    // Try to reinitialize UI if it's null
                    InitializeUI();
                }
            }
            catch (Exception ex)
            {
                GetLogger()?.LogError($"[UI] Error in ToggleUI: {ex}");
            }
        }
        
        // Enhanced logging method with rate limiting and debug control
        private static void LogDiagnostic(string message, BepInEx.Logging.LogLevel level = BepInEx.Logging.LogLevel.Info)
        {
            try
            {
                if (Logger == null)
                {
                    UnityEngine.Debug.Log($"[LethalMic] {message}");
                    return;
                }

                // Only check DebugLogging if it's not null and we're logging debug messages
                if (level == BepInEx.Logging.LogLevel.Debug && DebugLogging != null && !DebugLogging.Value)
                    return;

                var now = DateTime.Now;
                if ((now - LastLogTime).TotalMilliseconds < 100) // Rate limit to 10 logs per second
                {
                    LogCounter++;
                    if (LogCounter > 100) // If we're logging too much, skip
                        return;
                }
                else
                {
                    LogCounter = 0;
                    LastLogTime = now;
                }

                Logger.Log(level, $"[{now:HH:mm:ss.fff}] {message}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LethalMic] Error in LogDiagnostic: {ex}");
            }
        }
        
        // Harmony patches for game lifecycle events
        [HarmonyPatch(typeof(StartOfRound), "Start")]
        public static class StartOfRound_Start_Patch
        {
            public static void Postfix()
            {
                try
                {
                    GetLogger().LogInfo("Game started - initializing audio");
                    GetLogger().LogInfo($"Scene name: {SceneManager.GetActiveScene().name}");
                    StartRecording();
                }
                catch (Exception ex)
                {
                    GetLogger().LogError($"Error in game start patch: {ex}");
                    GetLogger().LogError($"Stack trace: {ex.StackTrace}");
                    errorCount++;
                }
            }
        }
        
        [HarmonyPatch(typeof(StartOfRound), "OnDestroy")]
        public static class StartOfRound_OnDestroy_Patch
        {
            public static void Prefix()
            {
                try
                {
                    GetLogger().LogInfo("Game quitting - stopping audio");
                    GetLogger().LogInfo($"Total audio frames processed: {audioFrameCount}");
                    GetLogger().LogInfo($"Total errors encountered: {errorCount}");
                    StopRecording();
                }
                catch (Exception ex)
                {
                    GetLogger().LogError($"Error in game quit patch: {ex}");
                    GetLogger().LogError($"Stack trace: {ex.StackTrace}");
                    errorCount++;
                }
            }
        }
        
        // Audio processing methods
        public static void StartRecording()
        {
            if (!IsInitialized || !EnableMod.Value || isRecording)
            {
                GetLogger().LogInfo($"StartRecording skipped - Initialized: {IsInitialized}, Enabled: {EnableMod.Value}, Recording: {isRecording}");
                return;
            }
                
            try
            {
                GetLogger().LogInfo("Starting microphone recording...");
                
                // Start microphone recording
                microphoneClip = Microphone.Start(selectedDevice, true, 10, 44100);
                isRecording = true;
                audioFrameCount = 0;
                
                GetLogger().LogInfo($"Recording started on device: {selectedDevice ?? "default"}");
                GetLogger().LogInfo($"Clip frequency: {microphoneClip.frequency}Hz");
                GetLogger().LogInfo($"Clip channels: {microphoneClip.channels}");
            }
            catch (Exception ex)
            {
                GetLogger().LogError($"Failed to start recording: {ex}");
                GetLogger().LogError($"Stack trace: {ex.StackTrace}");
                errorCount++;
            }
        }
        
        public static void StopRecording()
        {
            if (!isRecording)
            {
                GetLogger().LogInfo("StopRecording skipped - not recording");
                return;
            }
                
            try
            {
                GetLogger().LogInfo("Stopping microphone recording...");
                
                Microphone.End(selectedDevice);
                isRecording = false;
                
                if (microphoneClip != null)
                {
                    GetLogger().LogInfo($"Destroying audio clip - Length: {microphoneClip.length}s");
                    UnityEngine.Object.Destroy(microphoneClip);
                    microphoneClip = null;
                }
                
                GetLogger().LogInfo("Microphone recording stopped");
                GetLogger().LogInfo($"Total audio frames processed: {audioFrameCount}");
            }
            catch (Exception ex)
            {
                GetLogger().LogError($"Failed to stop recording: {ex}");
                GetLogger().LogError($"Stack trace: {ex.StackTrace}");
                errorCount++;
            }
        }
        
        // Throttling variables for logging
        private static float _lastLoggedLevel = -1f;
        private static bool _lastZeroWarningLogged = false;
        private static DateTime _lastAudioLog = DateTime.MinValue;
        
        public static void UpdateAudio()
        {
            if (!isRecording || microphoneClip == null) return;
            try
            {
                int micPos = Microphone.GetPosition(_selectedDeviceName);
                int windowSize = 256;
                int totalSamples = microphoneClip.samples;
                if (micPos < windowSize || totalSamples < windowSize)
                {
                    currentMicrophoneLevel = 0f;
                    return;
                }
                float[] window = new float[windowSize];
                int startPos = micPos - windowSize;
                if (startPos < 0) startPos += totalSamples;
                if (startPos + windowSize <= totalSamples)
                {
                    microphoneClip.GetData(window, startPos);
                }
                else
                {
                    int part1 = totalSamples - startPos;
                    int part2 = windowSize - part1;
                    float[] temp = new float[totalSamples];
                    microphoneClip.GetData(temp, 0);
                    // SAFETY CHECKS
                    if (part1 > 0 && part1 <= totalSamples && part1 <= windowSize &&
                        part2 >= 0 && part2 <= totalSamples && part2 <= windowSize)
                    {
                        Array.Copy(temp, startPos, window, 0, part1);
                        Array.Copy(temp, 0, window, part1, part2);
                    }
                    else
                    {
                        GetLogger().LogWarning($"[AUDIO] Skipping Array.Copy due to invalid part sizes: part1={part1}, part2={part2}, startPos={startPos}, totalSamples={totalSamples}, windowSize={windowSize}");
                        currentMicrophoneLevel = 0f;
                        return;
                    }
                }
                // Check if all zeroes - only warn once
                bool allZeroes = window.All(f => Math.Abs(f) < 1e-6);
                if (allZeroes && !_lastZeroWarningLogged)
                {
                    GetLogger().LogWarning("[AUDIO] Microphone buffer is all zeroes! Check device name and mic permissions.");
                    _lastZeroWarningLogged = true;
                }
                else if (!allZeroes && _lastZeroWarningLogged)
                {
                    GetLogger().LogInfo("[AUDIO] Microphone buffer now has valid data.");
                    _lastZeroWarningLogged = false;
                }
                float sum = 0f;
                for (int i = 0; i < window.Length; i++)
                    sum += window[i] * window[i];
                float rms = Mathf.Sqrt(sum / window.Length);
                currentMicrophoneLevel = rms * MicrophoneGain.Value;
                peakMicLevel = Mathf.Max(peakMicLevel * 0.95f, currentMicrophoneLevel);
                bool prevVoiceDetected = voiceDetected;
                voiceDetected = currentMicrophoneLevel > NoiseGateThreshold.Value;
                cpuUsage = Mathf.Lerp(cpuUsage, currentMicrophoneLevel * 100f, Time.deltaTime);
                // Only log when voiceDetected changes
                if (voiceDetected != prevVoiceDetected)
                {
                    float dbLevel = 20 * Mathf.Log10(Mathf.Max(currentMicrophoneLevel, 0.0001f));
                    if (voiceDetected)
                        GetLogger().LogInfo($"[AUDIO] Voice detected! Level: {currentMicrophoneLevel:F4} ({dbLevel:F1} dB)");
                    else
                        GetLogger().LogInfo($"[AUDIO] Voice no longer detected. Level: {currentMicrophoneLevel:F4} ({dbLevel:F1} dB)");
                }
                if (uiInstance != null)
                {
                    uiInstance.UpdateMicStatus("Connected", currentMicrophoneLevel);
                    uiInstance.UpdateCPUUsage(cpuUsage);
                }
                audioFrameCount++;
            }
            catch (Exception ex)
            {
                GetLogger().LogError($"Error processing microphone audio: {ex}");
                GetLogger().LogError($"Stack trace: {ex.StackTrace}");
                errorCount++;
                currentMicrophoneLevel = 0f;
            }
        }
        
        // Public methods for external access
        public static float GetCurrentMicrophoneLevel() => currentMicrophoneLevel;
        public static bool IsVoiceDetected() => voiceDetected;
        public static float GetNoiseFloor() => noiseFloor;
        public static float GetCPUUsage() => cpuUsage;
        public static int GetErrorCount() => errorCount;
        public static int GetAudioFrameCount() => audioFrameCount;

        public static bool IsUIVisible()
        {
            return uiInstance != null && uiInstance.IsVisible;
        }

        public static void Cleanup()
        {
            try
            {
                GetLogger().LogInfo("Cleaning up LethalMicStatic...");
                
                // Cleanup UI
                if (uiInstance != null)
                {
                    UnityEngine.Object.Destroy(uiInstance.gameObject);
                    uiInstance = null;
                }
                
                GetLogger().LogInfo("LethalMicStatic cleaned up");
            }
            catch (Exception ex)
            {
                GetLogger().LogError($"Error during cleanup: {ex}");
            }
        }

        private static ManualLogSource GetLogger()
        {
            return Logger;
        }

        // Add these methods for UI interaction
        public static float GetMicrophoneGain() => MicrophoneGain?.Value ?? 1.0f;
        public static void SetMicrophoneGain(float value) { if (MicrophoneGain != null) MicrophoneGain.Value = value; }
        public static float GetNoiseGateThreshold() => NoiseGateThreshold?.Value ?? 0.01f;
        public static void SetNoiseGateThreshold(float value) { if (NoiseGateThreshold != null) NoiseGateThreshold.Value = value; }
        public static bool GetNoiseGateEnabled() => NoiseGate?.Value ?? true;
        public static void SetNoiseGateEnabled(bool value) { if (NoiseGate != null) NoiseGate.Value = value; }
    }

    // Add this class at the end of the file, outside LethalMicStatic
    public class LethalMicUpdater : MonoBehaviour
    {
        void Update()
        {
            LethalMic.LethalMicStatic.UpdateAudio();
        }
    }
}