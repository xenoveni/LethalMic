using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using System;
using LethalCompanyInputUtils.Api;
using LethalMic;
using BepInEx.Logging;

namespace LethalMic.Patches;

[HarmonyPatch(typeof(PlayerControllerB), "Update")]
public static class PlayerControllerPatch
{
    // Input handling now managed by LethalMicInputActions
    // This patch can be used for other PlayerControllerB functionality if needed
    
    private static BepInEx.Logging.ManualLogSource Logger => (BepInEx.Logging.ManualLogSource)typeof(LethalMic.LethalMicStatic).GetField("Logger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);

    static PlayerControllerPatch()
    {
        Logger?.LogInfo("[PATCH] PlayerControllerPatch loaded");
    }

    [HarmonyPostfix]
    public static void UpdatePatch(PlayerControllerB __instance)
    {
        try
        {
            if (__instance == null || !__instance.isPlayerControlled || !__instance.IsOwner)
            {
                return;
            }

            // Input handling is now done through LethalMicInputActions
            if (LethalMicInputActions.Instance.ToggleUI.WasPressedThisFrame())
            {
                Logger?.LogInfo($"[INPUT] M key pressed at {DateTime.Now:HH:mm:ss.fff} - Toggle UI action triggered");
                LethalMicStatic.ToggleUI();
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError($"[PATCH] Error in PlayerControllerB patch: {ex}");
            Logger?.LogError($"[PATCH] Stack trace: {ex.StackTrace}");
        }
    }
}