using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;
using BepInEx.Logging;
using Dissonance;

namespace LethalMic.Patches
{
    /// <summary>
    /// Patches Lethal Company's voice chat system to apply our audio processing
    /// </summary>
    [HarmonyPatch]
    public static class VoiceChatPatch
    {
        private static ManualLogSource Logger;
        private static bool isInitialized = false;
        
        public static void Initialize(ManualLogSource logger)
        {
            Logger = logger;
            isInitialized = true;
            Logger.LogInfo("VoiceChatPatch initialized");
        }

        // Patch the internal WebRtcPreprocessingPipeline.PreprocessAudioFrame(float[]) method using reflection
        [HarmonyPatch]
        public static class WebRtcPreprocessingPipeline_PreprocessAudioFrame_Patch
        {
            static Type TargetType() => AccessTools.TypeByName("Dissonance.Audio.Capture.WebRtcPreprocessingPipeline");

            static MethodInfo TargetMethod()
            {
                var type = TargetType();
                return type?.GetMethod("PreprocessAudioFrame", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(float[]) }, null);
            }

            [HarmonyPrefix]
            public static bool Prefix(object __instance, float[] frame)
            {
                if (!isInitialized) return true;

                try
                {
                    Logger.LogInfo($"[LethalMic] Intercepted WebRtcPreprocessingPipeline frame: {frame?.Length ?? 0} samples");

                    // Apply your audio processing to the frame
                    if (frame != null && frame.Length > 0)
                    {
                        float[] processed = StaticAudioManager.ProcessAudioBuffer(frame);
                        Array.Copy(processed, frame, Math.Min(processed.Length, frame.Length));
                    }

                    // Let the original method run (so Dissonance can do its own processing too)
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error in WebRtcPreprocessingPipeline patch: {ex}");
                    return true;
                }
            }
        }

        // Old patches for reference (now obsolete)
        /*
        [HarmonyPatch(typeof(BasePreprocessingPipeline), "ProcessMicrophoneData")]
        public static class BasePreprocessingPipeline_ProcessMicrophoneData_Patch { ... }
        [HarmonyPatch(typeof(BasePreprocessingPipeline), "TransmitAudio")]
        public static class BasePreprocessingPipeline_TransmitAudio_Patch { ... }
        */

        // Processing helpers remain
        private static void ProcessAudioBuffer(float[] buffer, int offset, int length)
        {
            if (buffer == null || length <= 0) return;
            try
            {
                float[] audioData = new float[length];
                Array.Copy(buffer, offset, audioData, 0, length);
                float[] processedData = StaticAudioManager.ProcessAudioBuffer(audioData);
                Array.Copy(processedData, 0, buffer, offset, length);
                Logger.LogInfo($"Processed {length} audio samples through LethalMic pipeline");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error processing audio buffer: {ex}");
            }
        }
        private static void ProcessTransmissionBuffer(float[] buffer, int offset, int length)
        {
            if (buffer == null || length <= 0) return;
            try
            {
                Logger.LogInfo($"Final processing applied to {length} transmission samples");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error processing transmission buffer: {ex}");
            }
        }
    }
} 