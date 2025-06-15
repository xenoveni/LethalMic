using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace LethalMic.Patches;

public class PlayerControllerPatch
{
    private static bool _isUIVisible = false;
    private static LethalMicUI _uiComponent;

    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    [HarmonyPostfix]
    private static void UpdateHandleF8Key(PlayerControllerB __instance)
    {
        if (!__instance.isPlayerControlled || !__instance.IsOwner)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.F8))
        {
            ToggleUI();
        }
    }

    private static void ToggleUI()
    {
        if (_uiComponent == null)
        {
            var uiSystem = GameObject.Find("LethalMicUI");
            if (uiSystem != null)
            {
                _uiComponent = uiSystem.GetComponent<LethalMicUI>();
            }
        }

        if (_uiComponent != null)
        {
            _isUIVisible = !_isUIVisible;
            _uiComponent.ToggleVisibility();
        }
    }
} 