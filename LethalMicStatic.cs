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

namespace LethalMic
{
    /// <summary>
    /// Static implementation of LethalMic that doesn't rely on MonoBehaviour lifecycle
    /// This version uses static classes and Harmony patches to avoid destruction issues
    /// </summary>
    [BepInPlugin(PluginInfo.PLUGIN_GUID + ".Static", PluginInfo.PLUGIN_NAME + " (Static)", PluginInfo.PLUGIN_VERSION)]
    public class LethalMicStatic
    {
        // Static references
        private static ManualLogSource Logger;
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
        
        // Static constructor - called when the class is first accessed
        static LethalMicStatic()
        {
            try
            {
                Logger = BepInEx.Logging.Logger.CreateLogSource("LethalMicStatic");
                LogDiagnostic("Static constructor called", LogLevel.Info);
                LogDiagnostic($"Assembly location: {Assembly.GetExecutingAssembly().Location}", LogLevel.Debug);
                LogDiagnostic($"Unity version: {Application.unityVersion}", LogLevel.Debug);
                LogDiagnostic($"Game version: {Application.version}", LogLevel.Debug);
                
                // Initialize configuration
                ConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginInfo.PLUGIN_GUID + ".cfg"), true);
                InitializeConfig();
                
                // Apply Harmony patches
                HarmonyInstance = new Harmony(PluginInfo.PLUGIN_GUID + ".Static");
                LogDiagnostic("Applying Harmony patches...", LogLevel.Debug);
                HarmonyInstance.PatchAll();
                LogDiagnostic("Harmony patches applied successfully", LogLevel.Debug);
                
                // Initialize audio system
                InitializeAudio();
                
                IsInitialized = true;
                LogDiagnostic("Successfully initialized with static implementation", LogLevel.Info);
                LogDiagnostic($"Initialization completed at {DateTime.Now}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                LogDiagnostic($"Failed to initialize: {ex}", LogLevel.Error);
                LogDiagnostic($"Stack trace: {ex.StackTrace}", LogLevel.Error);
                errorCount++;
            }
        }
        
        private static void InitializeConfig()
        {
            try
            {
                LogDiagnostic("Initializing configuration...", LogLevel.Debug);
                
                EnableMod = ConfigFile.Bind("General", "EnableMod", true, "Enable/disable the LethalMic mod");
                MicrophoneGain = ConfigFile.Bind("Audio", "MicrophoneGain", 1.0f, "Microphone input gain (0.0 to 5.0)");
                InputDevice = ConfigFile.Bind("Audio", "InputDevice", "", "Preferred input device (empty for default)");
                NoiseGate = ConfigFile.Bind("Audio", "NoiseGate", true, "Enable noise gate");
                NoiseGateThreshold = ConfigFile.Bind("Audio", "NoiseGateThreshold", 0.01f, "Noise gate threshold (0.0 to 1.0)");
                DebugLogging = ConfigFile.Bind("Debug", "EnableDebugLogging", true, "Enable detailed debug logging");
                
                LogDiagnostic($"Configuration loaded - Enabled: {EnableMod.Value}, Gain: {MicrophoneGain.Value}", LogLevel.Info);
                LogDiagnostic($"Debug logging: {DebugLogging.Value}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                LogDiagnostic($"Failed to initialize configuration: {ex}", LogLevel.Error);
                LogDiagnostic($"Stack trace: {ex.StackTrace}", LogLevel.Error);
                errorCount++;
            }
        }
        
        private static void InitializeAudio()
        {
            if (!EnableMod.Value)
            {
                LogDiagnostic("Mod disabled in configuration", LogLevel.Info);
                return;
            }
            
            try
            {
                LogDiagnostic("Initializing audio system...", LogLevel.Debug);
                
                // Get available microphones
                var devices = Microphone.devices;
                LogDiagnostic($"Found {devices.Length} microphone devices", LogLevel.Info);
                
                foreach (var device in devices)
                {
                    LogDiagnostic($"Available device: {device}", LogLevel.Debug);
                }
                
                // Select device
                selectedDevice = string.IsNullOrEmpty(InputDevice.Value) ? null : InputDevice.Value;
                LogDiagnostic($"Selected device: {selectedDevice ?? "default"}", LogLevel.Info);
                
                // Test microphone access
                if (devices.Length > 0)
                {
                    LogDiagnostic("Audio system ready", LogLevel.Info);
                    LogDiagnostic($"Sample rate: {AudioSettings.outputSampleRate}Hz", LogLevel.Debug);
                    LogDiagnostic($"Speaker mode: {AudioSettings.speakerMode}", LogLevel.Debug);
                }
                else
                {
                    LogDiagnostic("No microphone devices found", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic($"Failed to initialize audio: {ex}", LogLevel.Error);
                LogDiagnostic($"Stack trace: {ex.StackTrace}", LogLevel.Error);
                errorCount++;
            }
        }
        
        // Enhanced logging method with rate limiting and debug control
        private static void LogDiagnostic(string message, LogLevel level = LogLevel.Info)
        {
            if (!DebugLogging.Value && level == LogLevel.Debug)
                return;
                
            var now = DateTime.Now;
            if ((now - LastLogTime).TotalSeconds < 1)
            {
                LogCounter++;
                if (LogCounter > 10)
                    return;
            }
            else
            {
                LogCounter = 0;
                LastLogTime = now;
            }
            
            Logger.Log(level, $"[STATIC] {message}");
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
    }
} 