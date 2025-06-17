using LethalCompanyInputUtils.Api;
using LethalCompanyInputUtils.BindingPathEnums;
using UnityEngine.InputSystem;

namespace LethalMic
{
    public class LethalMicInputActions : LcInputActions
    {
        public static readonly LethalMicInputActions Instance = new();

        public LethalMicInputActions()
        {
            BepInEx.Logging.Logger.CreateLogSource("LethalMicInputActions").LogInfo("[INPUT] LethalMicInputActions instance created");
        }

        public InputAction ToggleUI => Asset["toggleui"];
        public InputAction QuickMute => Asset["quickmute"];
        public InputAction PushToTalk => Asset["pushtotalk"];

        public override void CreateInputActions(in InputActionMapBuilder builder)
        {
            builder.NewActionBinding()
                .WithActionId("toggleui")
                .WithActionType(InputActionType.Button)
                .WithKeyboardControl(KeyboardControl.M)
                .WithBindingName("Toggle UI")
                .Finish();
            BepInEx.Logging.Logger.CreateLogSource("LethalMicInputActions").LogInfo("[INPUT] ToggleUI action bound to M key");

            builder.NewActionBinding()
                .WithActionId("quickmute")
                .WithActionType(InputActionType.Button)
                .WithKeyboardControl(KeyboardControl.F9)
                .WithBindingName("Quick Mute")
                .Finish();

            builder.NewActionBinding()
                .WithActionId("pushtotalk")
                .WithActionType(InputActionType.Button)
                .WithKeyboardControl(KeyboardControl.F10)
                .WithBindingName("Push to Talk")
                .Finish();
        }
    }
}