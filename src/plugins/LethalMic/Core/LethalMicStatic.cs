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
using Dissonance;

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
        // Remove unused fields to suppress warnings
        // private static AudioClip microphoneClip;
        private static string selectedDevice;
        private static bool isRecording = false;
        private static float currentMicrophoneLevel = -60f;
        private static float peakMicLevel = -60f;
        private static bool voiceDetected = false;
        private static float noiseFloor = -60f;
        private static float cpuUsage = 0f;
        private static int audioFrameCount = 0;
        private static int errorCount = 0;
        // Remove unused fields to suppress warnings
        // private static int lastMicPosition = 0;
        
        // Configuration
        private static ConfigEntry<bool> EnableMod;
        private static ConfigEntry<float> MicrophoneGain;
        private static ConfigEntry<string> InputDevice;
        private static ConfigEntry<bool> NoiseGate;
        private static ConfigEntry<float> NoiseGateThreshold;
        private static ConfigEntry<bool> DebugLogging;
        private static ConfigEntry<bool> Compression;
        private static ConfigEntry<float> CompressionRatio;
        private static ConfigEntry<float> AttackTime;
        private static ConfigEntry<float> ReleaseTime;
        private static ConfigEntry<EchoSuppressionMode> EchoMode;
        
        // UI
        private static GameObject uiObject;
        private static LethalMicUI uiInstance;
        private static GameObject updaterObject;
        
        // Throttle logging
        private static DateTime _lastSummaryLog = DateTime.MinValue;
        
        // Add for deduplicating/throttling Array.Copy errors
        private static HashSet<string> _arrayCopyErrors = new HashSet<string>();
        private static DateTime _lastArrayCopyLog = DateTime.MinValue;
        private static DateTime _lastAudioLog = DateTime.MinValue;
        
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
                NoiseGateThreshold = ConfigFile.Bind("Audio", "NoiseGateThreshold", 0.05f, "Noise gate threshold (0.0 to 1.0)");
                DebugLogging = ConfigFile.Bind("Debug", "DebugLogging", false, "Enable debug logging");
                Compression = ConfigFile.Bind("Audio", "Compression", true, "Enable audio compression");
                CompressionRatio = ConfigFile.Bind("Audio", "CompressionRatio", 10f, "Audio compression ratio (1:1 to 20:1)");
                AttackTime = ConfigFile.Bind("Audio", "AttackTime", 2f, "Compressor attack time in milliseconds (0-100)");
                ReleaseTime = ConfigFile.Bind("Audio", "ReleaseTime", 50f, "Compressor release time in milliseconds (0-1000)");
                EchoMode = ConfigFile.Bind("Audio", "EchoSuppressionMode", EchoSuppressionMode.Headphones, "Echo/Noise Suppression Mode: Headphones, StereoMix, WasapiLoopback");
                
                GetLogger().LogInfo($"Configuration loaded - Enabled: {EnableMod.Value}, Gain: {MicrophoneGain.Value}");
                
                // Initialize audio system
                InitializeAudio();
                
                // Apply Harmony patches
                HarmonyInstance = new Harmony("com.xenoveni.lethalmic");
                GetLogger().LogInfo("Applying Harmony patches...");
                HarmonyInstance.PatchAll();
                GetLogger().LogInfo("Harmony patches applied successfully");
                
                // Initialize voice chat patch
                LethalMic.Patches.VoiceChatPatch.Initialize(Logger);
                
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
                
                // Initialize the StaticAudioManager instead of direct microphone handling
                StaticAudioManager.Initialize(Logger);
                
                // Get available microphones
                var devices = Microphone.devices;
                GetLogger().LogInfo($"Found {devices.Length} microphone devices");
                
                if (devices.Length == 0)
                {
                    GetLogger().LogError("No microphone devices found. Please check your system settings and ensure a microphone is connected.");
                    if (uiInstance != null)
                    {
                        uiInstance.UpdateMicStatus("None", "No Devices", 0f);
                    }
                    return;
                }

                // Log all available devices
                foreach (var device in devices)
                {
                    GetLogger().LogInfo($"Available device: {device}");
                }
                
                // Validate and select device
                if (string.IsNullOrEmpty(InputDevice.Value))
                {
                    // If no device is selected in config, use the first available device
                    selectedDevice = devices[0];
                    GetLogger().LogInfo($"No device selected in config, using default device: {selectedDevice}");
                }
                else
                {
                    // Check if the configured device exists
                    if (devices.Contains(InputDevice.Value))
                    {
                        selectedDevice = InputDevice.Value;
                        GetLogger().LogInfo($"Using configured device: {selectedDevice}");
                    }
                    else
                    {
                        GetLogger().LogWarning($"Configured device '{InputDevice.Value}' not found, falling back to default device: {devices[0]}");
                        selectedDevice = devices[0];
                    }
                }

                // Start recording through StaticAudioManager
                StaticAudioManager.StartRecording();

                GetLogger().LogInfo($"Successfully initialized audio system with device: {selectedDevice}");
                
                // Update UI with success status
                if (uiInstance != null)
                {
                    uiInstance.UpdateMicStatus(selectedDevice, "Connected", 0f);
                }
            }
            catch (Exception ex)
            {
                GetLogger().LogError($"Error initializing audio system: {ex}");
                GetLogger().LogError($"Stack trace: {ex.StackTrace}");
                errorCount++;
                
                if (uiInstance != null)
                {
                    uiInstance.UpdateMicStatus("Error", "Initialization Failed", 0f);
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
                
                // Ensure updater is present
                if (updaterObject == null)
                {
                    updaterObject = new GameObject("LethalMicUpdater");
                    updaterObject.hideFlags = HideFlags.HideAndDontSave;
                    UnityEngine.Object.DontDestroyOnLoad(updaterObject);
                    updaterObject.AddComponent<LethalMicUpdater>();
                }
                
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
            if (!EnableMod.Value)
            {
                GetLogger().LogInfo("StartRecording skipped - mod disabled");
                return;
            }
            
            try
            {
                GetLogger().LogInfo("Starting microphone recording...");
                
                // Use StaticAudioManager for recording
                StaticAudioManager.StartRecording();
                isRecording = true;
                
                GetLogger().LogInfo("Microphone recording started successfully");
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
                
                // Use StaticAudioManager for stopping
                StaticAudioManager.StopRecording();
                isRecording = false;
                
                GetLogger().LogInfo("Microphone recording stopped");
            }
            catch (Exception ex)
            {
                GetLogger().LogError($"Failed to stop recording: {ex}");
                GetLogger().LogError($"Stack trace: {ex.StackTrace}");
                errorCount++;
            }
        }
        
        // Throttling variables for logging
        //private static float _lastLoggedLevel = -1f;
        
        public static void UpdateAudio()
        {
            if (!isRecording) return;

            try
            {
                // Use StaticAudioManager for audio processing
                StaticAudioManager.ProcessAudio();
                
                // Get updated values from StaticAudioManager
                currentMicrophoneLevel = StaticAudioManager.GetCurrentMicrophoneLevel();
                peakMicLevel = StaticAudioManager.GetPeakMicrophoneLevel();
                voiceDetected = StaticAudioManager.IsVoiceDetected();
                noiseFloor = StaticAudioManager.GetNoiseFloor();
                cpuUsage = StaticAudioManager.GetCPUUsage();
                
                audioFrameCount++;
            }
            catch (Exception ex)
            {
                GetLogger().LogError($"Error in UpdateAudio: {ex}");
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
                GetLogger().LogInfo("Cleaning up LethalMicStatic...");
                
                // Cleanup audio manager
                StaticAudioManager.Cleanup();
                
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
        public static void SetMicrophoneGain(float value) 
        { 
            if (MicrophoneGain != null) 
            {
                MicrophoneGain.Value = value;
                // Update processor settings when gain changes
                StaticAudioManager.UpdateProcessorSettings();
            }
        }
        
        public static float GetNoiseGateThreshold() => NoiseGateThreshold?.Value ?? 0.05f;
        public static void SetNoiseGateThreshold(float value) 
        { 
            if (NoiseGateThreshold != null) 
            {
                NoiseGateThreshold.Value = value;
                // Update processor settings when threshold changes
                StaticAudioManager.UpdateProcessorSettings();
            }
        }
        
        public static bool GetNoiseGateEnabled() => NoiseGate?.Value ?? true;
        public static void SetNoiseGateEnabled(bool value) 
        { 
            if (NoiseGate != null) 
            {
                NoiseGate.Value = value;
                // Update processor settings when noise gate changes
                StaticAudioManager.UpdateProcessorSettings();
            }
        }

        public static void SetInputDevice(string deviceName)
        {
            if (InputDevice != null) InputDevice.Value = deviceName;
            selectedDevice = deviceName;
            StaticAudioManager.SetInputDevice(deviceName);
        }

        public static string GetInputDevice() => selectedDevice;

        // Add compression-related methods
        public static bool GetCompressionEnabled() => Compression?.Value ?? true;
        public static void SetCompressionEnabled(bool value) 
        { 
            if (Compression != null) 
            {
                Compression.Value = value;
                // Update processor settings when compression changes
                StaticAudioManager.UpdateProcessorSettings();
            }
        }
        
        public static float GetCompressionRatio() => CompressionRatio?.Value ?? 10f;
        public static void SetCompressionRatio(float value) 
        { 
            if (CompressionRatio != null) 
            {
                CompressionRatio.Value = value;
                // Update processor settings when compression ratio changes
                StaticAudioManager.UpdateProcessorSettings();
            }
        }
        
        public static float GetAttackTime() => AttackTime?.Value ?? 2f;
        public static void SetAttackTime(float value) 
        { 
            if (AttackTime != null) 
            {
                AttackTime.Value = value;
                // Update processor settings when attack time changes
                StaticAudioManager.UpdateProcessorSettings();
            }
        }
        
        public static float GetReleaseTime() => ReleaseTime?.Value ?? 50f;
        public static void SetReleaseTime(float value) 
        { 
            if (ReleaseTime != null) 
            {
                ReleaseTime.Value = value;
                // Update processor settings when release time changes
                StaticAudioManager.UpdateProcessorSettings();
            }
        }

        // Method to get UI instance for StaticAudioManager
        public static LethalMicUI GetUIIInstance() => uiInstance;

        // Add EchoSuppressionMode enum
        public enum EchoSuppressionMode
        {
            Headphones,
            StereoMix,
            WasapiLoopback
        }

        // In UpdateProcessorSettings or wherever processing is selected:
        private static void UpdateProcessorSettings()
        {
            switch (EchoMode.Value)
            {
                case EchoSuppressionMode.Headphones:
                    // Aggressive noise gate, suppression, compression
                    StaticAudioManager.SetAggressiveSuppression();
                    break;
                case EchoSuppressionMode.StereoMix:
                    // Try to use Stereo Mix as reference for echo cancellation
                    StaticAudioManager.SetStereoMixSuppression();
                    break;
                case EchoSuppressionMode.WasapiLoopback:
                    // Try to use WASAPI loopback (NAudio) as reference for echo cancellation
                    StaticAudioManager.SetWasapiSuppression();
                    break;
            }
        }

        public static EchoSuppressionMode GetEchoSuppressionMode() => EchoMode?.Value ?? EchoSuppressionMode.Headphones;
        public static void SetEchoSuppressionMode(EchoSuppressionMode mode)
        {
            if (EchoMode != null)
            {
                EchoMode.Value = mode;
                UpdateProcessorSettings();
            }
        }
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