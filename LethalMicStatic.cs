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
using static LethalMic.PluginInfo;

namespace LethalMic
{
    /// <summary>
    /// Static implementation of LethalMic that doesn't rely on MonoBehaviour lifecycle
    /// This version uses static classes and Harmony patches to avoid destruction issues
    /// </summary>
    [BepInPlugin(PluginInfo.PLUGIN_GUID + ".Static", PluginInfo.PLUGIN_NAME + " (Static)", PluginInfo.PLUGIN_VERSION)]
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

                LogDiagnostic("Initializing LethalMic static class...", BepInEx.Logging.LogLevel.Info);
                
                // Initialize configuration
                ConfigFile = Config;  // Use the base class Config property
                
                // Load settings
                EnableMod = ConfigFile.Bind("General", "EnableMod", true, "Enable/disable the LethalMic mod");
                MicrophoneGain = ConfigFile.Bind("Audio", "MicrophoneGain", 1.0f, "Microphone input gain (0.0 to 5.0)");
                InputDevice = ConfigFile.Bind("Audio", "InputDevice", "", "Preferred input device (empty for default)");
                NoiseGate = ConfigFile.Bind("Audio", "NoiseGate", true, "Enable noise gate");
                NoiseGateThreshold = ConfigFile.Bind("Audio", "NoiseGateThreshold", 0.01f, "Noise gate threshold (0.0 to 1.0)");
                DebugLogging = ConfigFile.Bind("Debug", "DebugLogging", false, "Enable debug logging");
                
                LogDiagnostic($"Configuration loaded - Enabled: {EnableMod.Value}, Gain: {MicrophoneGain.Value}", BepInEx.Logging.LogLevel.Info);
                
                // Initialize audio system
                InitializeAudio();
                
                // Apply Harmony patches
                HarmonyInstance = new Harmony("com.xenoveni.lethalmic");
                LogDiagnostic("Applying Harmony patches...", BepInEx.Logging.LogLevel.Debug);
                HarmonyInstance.PatchAll();
                LogDiagnostic("Harmony patches applied successfully", BepInEx.Logging.LogLevel.Debug);
                
                // Initialize UI
                InitializeUI();
                
                IsInitialized = true;
                LogDiagnostic("Successfully initialized with static implementation", BepInEx.Logging.LogLevel.Info);
                LogDiagnostic($"Initialization completed at {DateTime.Now}", BepInEx.Logging.LogLevel.Debug);
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
        
        private static void InitializeAudio()
        {
            if (!EnableMod.Value)
            {
                LogDiagnostic("Mod disabled in configuration", BepInEx.Logging.LogLevel.Info);
                return;
            }
            
            try
            {
                LogDiagnostic("Initializing audio system...", BepInEx.Logging.LogLevel.Debug);
                
                // Get available microphones
                var devices = Microphone.devices;
                LogDiagnostic($"Found {devices.Length} microphone devices", BepInEx.Logging.LogLevel.Info);
                
                foreach (var device in devices)
                {
                    LogDiagnostic($"Available device: {device}", BepInEx.Logging.LogLevel.Debug);
                }
                
                // Select device
                selectedDevice = string.IsNullOrEmpty(InputDevice.Value) ? null : InputDevice.Value;
                LogDiagnostic($"Selected device: {selectedDevice ?? "default"}", BepInEx.Logging.LogLevel.Info);
                
                // Test microphone access
                if (devices.Length > 0)
                {
                    LogDiagnostic("Audio system ready", BepInEx.Logging.LogLevel.Info);
                    LogDiagnostic($"Sample rate: {AudioSettings.outputSampleRate}Hz", BepInEx.Logging.LogLevel.Debug);
                    LogDiagnostic($"Speaker mode: {AudioSettings.speakerMode}", BepInEx.Logging.LogLevel.Debug);
                }
                else
                {
                    LogDiagnostic("No microphone devices found", BepInEx.Logging.LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic($"Failed to initialize audio: {ex}", BepInEx.Logging.LogLevel.Error);
                LogDiagnostic($"Stack trace: {ex.StackTrace}", BepInEx.Logging.LogLevel.Error);
                errorCount++;
            }
        }
        
        private static void InitializeUI()
        {
            try
            {
                Logger.LogInfo("Initializing UI...");
                
                // Create UI GameObject
                uiObject = new GameObject("LethalMicUI");
                uiObject.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(uiObject);
                
                // Add UI component
                uiInstance = uiObject.AddComponent<LethalMicUI>();
                uiInstance.Initialize(Logger, ConfigFile);
                
                // Ensure UI stays active
                uiObject.SetActive(true);
                
                Logger.LogInfo("UI initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize UI: {ex}");
            }
        }
        
        public static void ToggleUI()
        {
            try
            {
                LogDiagnostic("=== UI Toggle Requested ===", BepInEx.Logging.LogLevel.Info);
                
                if (uiInstance == null)
                {
                    LogDiagnostic("UI instance is null, attempting to initialize", BepInEx.Logging.LogLevel.Warning);
                    InitializeUI();
                }
                
                if (uiInstance != null)
                {
                    LogDiagnostic($"Current UI visibility: {uiInstance.IsVisible}", BepInEx.Logging.LogLevel.Info);
                    uiInstance.ToggleVisibility();
                    LogDiagnostic($"New UI visibility: {uiInstance.IsVisible}", BepInEx.Logging.LogLevel.Info);
                }
                else
                {
                    LogDiagnostic("Failed to initialize UI instance", BepInEx.Logging.LogLevel.Error);
                }
                
                LogDiagnostic("=== UI Toggle Completed ===", BepInEx.Logging.LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogDiagnostic($"Error toggling UI: {ex}", BepInEx.Logging.LogLevel.Error);
                LogDiagnostic($"Stack trace: {ex.StackTrace}", BepInEx.Logging.LogLevel.Error);
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
                    LogDiagnostic("Game started - initializing audio", LogLevel.Info);
                    LogDiagnostic($"Scene name: {SceneManager.GetActiveScene().name}", LogLevel.Debug);
                    StartRecording();
                }
                catch (Exception ex)
                {
                    LogDiagnostic($"Error in game start patch: {ex}", LogLevel.Error);
                    LogDiagnostic($"Stack trace: {ex.StackTrace}", LogLevel.Error);
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
                    LogDiagnostic("Game quitting - stopping audio", LogLevel.Info);
                    LogDiagnostic($"Total audio frames processed: {audioFrameCount}", LogLevel.Debug);
                    LogDiagnostic($"Total errors encountered: {errorCount}", LogLevel.Debug);
                    StopRecording();
                }
                catch (Exception ex)
                {
                    LogDiagnostic($"Error in game quit patch: {ex}", LogLevel.Error);
                    LogDiagnostic($"Stack trace: {ex.StackTrace}", LogLevel.Error);
                    errorCount++;
                }
            }
        }
        
        // Audio processing methods
        public static void StartRecording()
        {
            if (!IsInitialized || !EnableMod.Value || isRecording)
            {
                LogDiagnostic($"StartRecording skipped - Initialized: {IsInitialized}, Enabled: {EnableMod.Value}, Recording: {isRecording}", LogLevel.Debug);
                return;
            }
                
            try
            {
                LogDiagnostic("Starting microphone recording...", LogLevel.Info);
                
                // Start microphone recording
                microphoneClip = Microphone.Start(selectedDevice, true, 10, 44100);
                isRecording = true;
                audioFrameCount = 0;
                
                LogDiagnostic($"Recording started on device: {selectedDevice ?? "default"}", LogLevel.Info);
                LogDiagnostic($"Clip frequency: {microphoneClip.frequency}Hz", LogLevel.Debug);
                LogDiagnostic($"Clip channels: {microphoneClip.channels}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                LogDiagnostic($"Failed to start recording: {ex}", LogLevel.Error);
                LogDiagnostic($"Stack trace: {ex.StackTrace}", LogLevel.Error);
                errorCount++;
            }
        }
        
        public static void StopRecording()
        {
            if (!isRecording)
            {
                LogDiagnostic("StopRecording skipped - not recording", LogLevel.Debug);
                return;
            }
                
            try
            {
                LogDiagnostic("Stopping microphone recording...", LogLevel.Info);
                
                Microphone.End(selectedDevice);
                isRecording = false;
                
                if (microphoneClip != null)
                {
                    LogDiagnostic($"Destroying audio clip - Length: {microphoneClip.length}s", LogLevel.Debug);
                    UnityEngine.Object.Destroy(microphoneClip);
                    microphoneClip = null;
                }
                
                LogDiagnostic("Microphone recording stopped", LogLevel.Info);
                LogDiagnostic($"Total audio frames processed: {audioFrameCount}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                LogDiagnostic($"Failed to stop recording: {ex}", LogLevel.Error);
                LogDiagnostic($"Stack trace: {ex.StackTrace}", LogLevel.Error);
                errorCount++;
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
                Logger.LogInfo("Cleaning up LethalMicStatic...");
                
                // Cleanup UI
                if (uiInstance != null)
                {
                    UnityEngine.Object.Destroy(uiInstance.gameObject);
                    uiInstance = null;
                }
                
                Logger.LogInfo("LethalMicStatic cleaned up");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error during cleanup: {ex}");
            }
        }
    }
}