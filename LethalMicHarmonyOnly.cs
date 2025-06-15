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

namespace LethalMic
{
    /// <summary>
    /// Harmony-only implementation that avoids BaseUnityPlugin lifecycle issues
    /// This approach uses static classes and minimal GameObject dependencies
    /// </summary>
    [BepInPlugin(PluginInfo.PLUGIN_GUID + ".HarmonyOnly", PluginInfo.PLUGIN_NAME + " (Harmony Only)", PluginInfo.PLUGIN_VERSION)]
    public class LethalMicHarmonyOnly : BaseUnityPlugin
    {
        public static new ManualLogSource Logger;
        private static Harmony HarmonyInstance;
        private static ConfigFile ConfigFile;
        
        void Awake()
        {
            Logger = base.Logger;
            ConfigFile = Config;
            
            Logger.LogInfo("[HARMONY-ONLY] LethalMic Harmony-Only version starting...");
            Logger.LogInfo("[HARMONY-ONLY] This version avoids BaseUnityPlugin lifecycle issues");
            
            try
            {
                // Initialize static systems
                StaticAudioManager.Initialize(Logger, ConfigFile);
                
                // Apply Harmony patches
                HarmonyInstance = new Harmony(PluginInfo.PLUGIN_GUID + ".HarmonyOnly");
                HarmonyInstance.PatchAll();
                
                Logger.LogInfo("[HARMONY-ONLY] Successfully initialized with Harmony patches");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[HARMONY-ONLY] Failed to initialize: {ex}");
            }
        }
        
        void OnDestroy()
        {
            Logger?.LogInfo("[HARMONY-ONLY] Plugin being destroyed - cleaning up...");
            StaticAudioManager.Cleanup();
            HarmonyInstance?.UnpatchSelf();
        }
    }
    
    /// <summary>
    /// Static audio manager that doesn't rely on MonoBehaviour lifecycle
    /// </summary>
    public static class StaticAudioManager
    {
        private static ManualLogSource Logger;
        private static ConfigFile ConfigFile;
        private static bool IsInitialized = false;
        
        // Configuration
        private static ConfigEntry<bool> EnableMod;
        private static ConfigEntry<float> MicrophoneGain;
        private static ConfigEntry<string> InputDevice;
        private static ConfigEntry<bool> NoiseGate;
        private static ConfigEntry<float> NoiseGateThreshold;
        
        // Audio processing
        private static AudioClip microphoneClip;
        private static string selectedDevice;
        private static bool isRecording = false;
        
        public static void Initialize(ManualLogSource logger, ConfigFile configFile)
        {
            if (IsInitialized)
            {
                logger.LogWarning("[STATIC] StaticAudioManager already initialized");
                return;
            }
            
            Logger = logger;
            ConfigFile = configFile;
            
            Logger.LogInfo("[STATIC] Initializing StaticAudioManager...");
            
            try
            {
                // Initialize configuration
                InitializeConfig();
                
                // Initialize audio systems
                InitializeAudio();
                
                IsInitialized = true;
                Logger.LogInfo("[STATIC] StaticAudioManager initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[STATIC] Failed to initialize StaticAudioManager: {ex}");
            }
        }
        
        private static void InitializeConfig()
        {
            EnableMod = ConfigFile.Bind("General", "EnableMod", true, "Enable/disable the LethalMic mod");
            MicrophoneGain = ConfigFile.Bind("Audio", "MicrophoneGain", 1.0f, "Microphone input gain (0.0 to 5.0)");
            InputDevice = ConfigFile.Bind("Audio", "InputDevice", "", "Preferred input device (empty for default)");
            NoiseGate = ConfigFile.Bind("Audio", "NoiseGate", true, "Enable noise gate");
            NoiseGateThreshold = ConfigFile.Bind("Audio", "NoiseGateThreshold", 0.01f, "Noise gate threshold (0.0 to 1.0)");
            
            Logger.LogInfo($"[STATIC] Configuration loaded - Enabled: {EnableMod.Value}, Gain: {MicrophoneGain.Value}");
        }
        
        private static void InitializeAudio()
        {
            if (!EnableMod.Value)
            {
                Logger.LogInfo("[STATIC] Mod disabled in configuration");
                return;
            }
            
            try
            {
                // Get available microphones
                var devices = Microphone.devices;
                Logger.LogInfo($"[STATIC] Found {devices.Length} microphone devices");
                
                foreach (var device in devices)
                {
                    Logger.LogInfo($"[STATIC] Available device: {device}");
                }
                
                // Select device
                selectedDevice = string.IsNullOrEmpty(InputDevice.Value) ? null : InputDevice.Value;
                Logger.LogInfo($"[STATIC] Selected device: {selectedDevice ?? "default"}");
                
                // Test microphone access
                if (devices.Length > 0)
                {
                    Logger.LogInfo("[STATIC] Audio system ready");
                }
                else
                {
                    Logger.LogWarning("[STATIC] No microphone devices found");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[STATIC] Failed to initialize audio: {ex}");
            }
        }
        
        public static void StartRecording()
        {
            if (!IsInitialized || !EnableMod.Value || isRecording)
                return;
                
            try
            {
                Logger.LogInfo("[STATIC] Starting microphone recording...");
                
                // Start microphone recording
                microphoneClip = Microphone.Start(selectedDevice, true, 10, 44100);
                isRecording = true;
                
                Logger.LogInfo("[STATIC] Microphone recording started");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[STATIC] Failed to start recording: {ex}");
            }
        }
        
        public static void StopRecording()
        {
            if (!isRecording)
                return;
                
            try
            {
                Logger.LogInfo("[STATIC] Stopping microphone recording...");
                
                Microphone.End(selectedDevice);
                isRecording = false;
                
                if (microphoneClip != null)
                {
                    UnityEngine.Object.Destroy(microphoneClip);
                    microphoneClip = null;
                }
                
                Logger.LogInfo("[STATIC] Microphone recording stopped");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[STATIC] Failed to stop recording: {ex}");
            }
        }
        
        public static void Cleanup()
        {
            Logger?.LogInfo("[STATIC] Cleaning up StaticAudioManager...");
            
            try
            {
                StopRecording();
                IsInitialized = false;
                Logger?.LogInfo("[STATIC] StaticAudioManager cleaned up");
            }
            catch (Exception ex)
            {
                Logger?.LogError($"[STATIC] Error during cleanup: {ex}");
            }
        }
    }
    
    /// <summary>
    /// Harmony patches for game integration
    /// </summary>
    [HarmonyPatch]
    public static class GamePatches
    {
        private static ManualLogSource Logger => LethalMicHarmonyOnly.Logger;
        
        /// <summary>
        /// Patch game start to initialize our systems
        /// </summary>
        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        public static void StartOfRound_Awake_Postfix(StartOfRound __instance)
        {
            try
            {
                Logger?.LogInfo("[PATCH] StartOfRound.Awake - Game starting");
                StaticAudioManager.StartRecording();
            }
            catch (Exception ex)
            {
                Logger?.LogError($"[PATCH] Error in StartOfRound.Awake patch: {ex}");
            }
        }
        
        /// <summary>
        /// Patch game end to cleanup our systems
        /// </summary>
        [HarmonyPatch(typeof(StartOfRound), "OnDestroy")]
        [HarmonyPostfix]
        public static void StartOfRound_OnDestroy_Postfix(StartOfRound __instance)
        {
            try
            {
                Logger?.LogInfo("[PATCH] StartOfRound.OnDestroy - Game ending");
                StaticAudioManager.StopRecording();
            }
            catch (Exception ex)
            {
                Logger?.LogError($"[PATCH] Error in StartOfRound.OnDestroy patch: {ex}");
            }
        }
        
        /// <summary>
        /// Patch player voice chat to integrate our audio
        /// </summary>
        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPostfix]
        public static void PlayerControllerB_Update_Postfix(PlayerControllerB __instance)
        {
            // Only process for local player
            if (!__instance.IsOwner)
                return;
                
            try
            {
                // Add our audio processing logic here
                // This runs every frame for the local player
            }
            catch (Exception ex)
            {
                Logger?.LogError($"[PATCH] Error in PlayerControllerB.Update patch: {ex}");
            }
        }
    }
}