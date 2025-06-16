using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using System;
// Input handling now managed by LethalMicInputActions

namespace LethalMic.Patches;

public class PlayerControllerPatch
{
    // Input handling now managed by LethalMicInputActions
    // This patch can be used for other PlayerControllerB functionality if needed
    
    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    [HarmonyPostfix]
    private static void UpdatePatch(PlayerControllerB __instance)
    {
        // Placeholder for future PlayerControllerB patches
        // Input handling is now done through LethalMicInputActions
        try
        {
            // Any other PlayerControllerB update logic can go here
        }
        catch (Exception ex)
        {
            LethalMic.GetLogger()?.LogError($"[PATCH] Error in PlayerControllerB patch: {ex}");
        }
    }
}