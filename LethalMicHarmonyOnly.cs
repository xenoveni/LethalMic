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
using static LethalMic.PluginInfo;

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
        private static Harmony harmony;
        private static ConfigFile ConfigFile;
        private static GameObject uiObject;
        public static LethalMicUI uiComponent;
        
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
                
                // Initialize UI
                InitializeUI();
                
                // Apply Harmony patches
                harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".HarmonyOnly");
                harmony.PatchAll();
                
                Logger.LogInfo("[HARMONY-ONLY] Successfully initialized with Harmony patches");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[HARMONY-ONLY] Failed to initialize: {ex}");
            }
        }
        
        private void InitializeUI()
        {
            try
            {
                Logger.LogInfo("[HARMONY-ONLY] Initializing UI...");
                
                // Create UI GameObject
                uiObject = new GameObject("LethalMicUI");
                uiObject.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(uiObject);
                
                // Add UI component
                uiComponent = uiObject.AddComponent<LethalMicUI>();
                uiComponent.Initialize(Logger, ConfigFile);
                
                // Ensure UI stays active
                uiObject.SetActive(true);
                
                Logger.LogInfo("[HARMONY-ONLY] UI initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[HARMONY-ONLY] Failed to initialize UI: {ex}");
            }
        }
        
        void OnDestroy()
        {
            Logger?.LogInfo("[HARMONY-ONLY] Plugin being destroyed - cleaning up...");
            
            // Clean up UI
            if (uiObject != null)
            {
                UnityEngine.Object.Destroy(uiObject);
                uiObject = null;
                uiComponent = null;
            }
            
            StaticAudioManager.Cleanup();
            harmony?.UnpatchSelf();
        }
    }
    
    /// <summary>
    /// Harmony patches for game integration
    /// </summary>
    [HarmonyPatch]
    public static class GamePatches
    {
        private static ManualLogSource Logger => LethalMicHarmonyOnly.Logger;
        private static LethalMicUI UI => LethalMicHarmonyOnly.uiComponent;
        
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
        
        /// <summary>
        /// Patch player controller to handle F8 key
        /// </summary>
        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPostfix]
        public static void PlayerControllerB_Update_Postfix_F8(PlayerControllerB __instance)
        {
            try
            {
                // Input handling now managed by LethalMicInputActions
                // This legacy input handling is no longer needed
            }
            catch (Exception ex)
            {
                Logger?.LogError($"[PATCH] Error in PlayerControllerB.Update patch: {ex}");
            }
        }
    }
}