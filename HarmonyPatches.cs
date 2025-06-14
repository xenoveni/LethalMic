using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace LethalMic
{
    /// <summary>
    /// Harmony patches to intercept game audio and microphone input
    /// </summary>
    public static class HarmonyPatches
    {
        private static LethalMic _pluginInstance;
        private static readonly Dictionary<AudioSource, AudioSourceData> _trackedAudioSources = new Dictionary<AudioSource, AudioSourceData>();
        private static readonly List<float[]> _gameAudioBuffers = new List<float[]>();
        private static readonly object _audioBufferLock = new object();
        
        public static void Initialize(LethalMic pluginInstance)
        {
            _pluginInstance = pluginInstance;
        }
        
        /// <summary>
        /// Patch AudioSource.Play to track game audio sources
        /// </summary>
        [HarmonyPatch(typeof(AudioSource), "Play", new Type[] { })]
        public static class AudioSource_Play_Patch
        {
            public static void Postfix(AudioSource __instance)
            {
                try
                {
                    if (__instance != null && __instance.clip != null)
                    {
                        TrackAudioSource(__instance);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LethalMic] Error in AudioSource.Play patch: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Patch AudioSource.PlayOneShot to track one-shot audio
        /// </summary>
        [HarmonyPatch(typeof(AudioSource), "PlayOneShot", new Type[] { typeof(AudioClip) })]
        public static class AudioSource_PlayOneShot_Patch
        {
            public static void Postfix(AudioSource __instance, AudioClip clip)
            {
                try
                {
                    if (__instance != null && clip != null)
                    {
                        TrackAudioSource(__instance, clip);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LethalMic] Error in AudioSource.PlayOneShot patch: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Patch AudioListener.GetOutputData to capture final mixed audio
        /// </summary>
        [HarmonyPatch(typeof(AudioListener), "GetOutputData", new Type[] { typeof(float[]), typeof(int) })]
        public static class AudioListener_GetOutputData_Patch
        {
            public static void Postfix(float[] samples, int channel)
            {
                try
                {
                    if (samples != null && samples.Length > 0 && _pluginInstance != null)
                    {
                        // Store a copy of the audio data for echo cancellation
                        float[] audioData = new float[samples.Length];
                        Array.Copy(samples, audioData, samples.Length);
                        
                        lock (_audioBufferLock)
                        {
                            _gameAudioBuffers.Add(audioData);
                            
                            // Keep only recent buffers to prevent memory buildup
                            while (_gameAudioBuffers.Count > 10)
                            {
                                _gameAudioBuffers.RemoveAt(0);
                            }
                        }
                        
                        // Notify plugin of game audio
                        _pluginInstance.OnGameAudioCaptured(audioData, channel);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LethalMic] Error in AudioListener.GetOutputData patch: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Patch AudioListener.GetSpectrumData to capture frequency domain data
        /// </summary>
        [HarmonyPatch(typeof(AudioListener), "GetSpectrumData", new Type[] { typeof(float[]), typeof(int), typeof(FFTWindow) })]
        public static class AudioListener_GetSpectrumData_Patch
        {
            public static void Postfix(float[] samples, int channel, FFTWindow window)
            {
                try
                {
                    if (samples != null && samples.Length > 0 && _pluginInstance != null)
                    {
                        // Store spectrum data for advanced processing
                        float[] spectrumData = new float[samples.Length];
                        Array.Copy(samples, spectrumData, samples.Length);
                        
                        _pluginInstance.OnGameSpectrumCaptured(spectrumData, channel);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LethalMic] Error in AudioListener.GetSpectrumData patch: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Patch Microphone.Start to intercept microphone initialization
        /// </summary>
        [HarmonyPatch(typeof(Microphone), "Start", new Type[] { typeof(string), typeof(bool), typeof(int), typeof(int) })]
        public static class Microphone_Start_Patch
        {
            public static void Postfix(string deviceName, bool loop, int lengthSec, int frequency, AudioClip __result)
            {
                try
                {
                    if (__result != null && _pluginInstance != null)
                    {
                        Debug.Log($"[LethalMic] Microphone started: {deviceName}, Frequency: {frequency}, Length: {lengthSec}s");
                        _pluginInstance.OnMicrophoneStarted(deviceName, frequency, lengthSec, __result);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LethalMic] Error in Microphone.Start patch: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Patch Microphone.End to handle microphone cleanup
        /// </summary>
        [HarmonyPatch(typeof(Microphone), "End", new Type[] { typeof(string) })]
        public static class Microphone_End_Patch
        {
            public static void Postfix(string deviceName)
            {
                try
                {
                    if (_pluginInstance != null)
                    {
                        Debug.Log($"[LethalMic] Microphone ended: {deviceName}");
                        _pluginInstance.OnMicrophoneEnded(deviceName);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LethalMic] Error in Microphone.End patch: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Patch AudioClip.GetData to intercept audio data reading
        /// </summary>
        [HarmonyPatch(typeof(AudioClip), "GetData", new Type[] { typeof(float[]), typeof(int) })]
        public static class AudioClip_GetData_Patch
        {
            public static void Postfix(AudioClip __instance, float[] data, int offsetSamples, bool __result)
            {
                try
                {
                    if (__result && data != null && data.Length > 0 && _pluginInstance != null)
                    {
                        // Check if this is microphone data
                        if (IsMicrophoneClip(__instance))
                        {
                            // Process microphone audio data
                            float[] micData = new float[data.Length];
                            Array.Copy(data, micData, data.Length);
                            _pluginInstance.OnMicrophoneDataCaptured(micData, __instance);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LethalMic] Error in AudioClip.GetData patch: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Patch AudioClip.SetData to intercept audio data writing
        /// </summary>
        [HarmonyPatch(typeof(AudioClip), "SetData", new Type[] { typeof(float[]), typeof(int) })]
        public static class AudioClip_SetData_Patch
        {
            public static bool Prefix(AudioClip __instance, float[] data, int offsetSamples)
            {
                try
                {
                    if (data != null && data.Length > 0 && _pluginInstance != null)
                    {
                        // Check if this is microphone data being written
                        if (IsMicrophoneClip(__instance))
                        {
                            // Process the data before it's written to the clip
                            _pluginInstance.ProcessMicrophoneAudio(data, 1); // Assume mono audio
                            // Data is processed in-place
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LethalMic] Error in AudioClip.SetData patch: {ex.Message}");
                }
                
                return true; // Continue with original method
            }
        }
        
        /// <summary>
        /// Patch Unity's internal audio processing for voice chat
        /// </summary>
        // Note: Dynamic voice chat patching removed to prevent Harmony parameter mismatch errors
        // Voice chat audio will be captured through other available patches
        
        // Helper methods
        private static void TrackAudioSource(AudioSource audioSource, AudioClip clip = null)
        {
            if (audioSource == null) return;
            
            var clipToUse = clip ?? audioSource.clip;
            if (clipToUse == null) return;
            
            if (!_trackedAudioSources.ContainsKey(audioSource))
            {
                _trackedAudioSources[audioSource] = new AudioSourceData
                {
                    AudioSource = audioSource,
                    Clip = clipToUse,
                    StartTime = Time.time,
                    IsPlaying = true
                };
                
                Debug.Log($"[LethalMic] Tracking audio source: {clipToUse.name}");
            }
        }
        
        private static bool IsMicrophoneClip(AudioClip clip)
        {
            if (clip == null) return false;
            
            // Check if this clip was created by Microphone.Start
            // Unity microphone clips typically have specific characteristics
            return clip.name.Contains("Microphone") || 
                   clip.loadType == AudioClipLoadType.Streaming ||
                   (clip.length > 10f && clip.channels <= 2); // Long clips with few channels are likely microphone
        }
        
        public static float[] GetLatestGameAudio()
        {
            lock (_audioBufferLock)
            {
                if (_gameAudioBuffers.Count > 0)
                {
                    var latest = _gameAudioBuffers[_gameAudioBuffers.Count - 1];
                    float[] result = new float[latest.Length];
                    Array.Copy(latest, result, latest.Length);
                    return result;
                }
            }
            return new float[0];
        }
        
        public static void CleanupTrackedSources()
        {
            var toRemove = new List<AudioSource>();
            
            foreach (var kvp in _trackedAudioSources)
            {
                if (kvp.Key == null || !kvp.Key.isPlaying)
                {
                    toRemove.Add(kvp.Key);
                }
            }
            
            foreach (var source in toRemove)
            {
                _trackedAudioSources.Remove(source);
            }
        }
        
        public static List<AudioSourceData> GetActiveAudioSources()
        {
            CleanupTrackedSources();
            return _trackedAudioSources.Values.Where(data => data.IsPlaying).ToList();
        }
    }
    
    /// <summary>
    /// Data structure to track audio source information
    /// </summary>
    public class AudioSourceData
    {
        public AudioSource AudioSource { get; set; }
        public AudioClip Clip { get; set; }
        public float StartTime { get; set; }
        public bool IsPlaying { get; set; }
        public float Volume => AudioSource?.volume ?? 0f;
        public float Pitch => AudioSource?.pitch ?? 1f;
        public Vector3 Position => AudioSource?.transform?.position ?? Vector3.zero;
        public bool Is3D => AudioSource?.spatialBlend > 0f;
    }
}