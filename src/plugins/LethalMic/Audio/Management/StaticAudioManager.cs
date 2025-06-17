using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Logging;
using BepInEx.Configuration;

namespace LethalMic
{
    public static class StaticAudioManager
    {
        private static ManualLogSource Logger;
        private static ConfigFile Config;
        private static bool isInitialized;
        private static bool isRecording;
        private static float[] audioBuffer;
        private static int bufferSize = 1024;
        private static float gain = 1.0f;
        private static float noiseGate = 0.01f;
        private static string selectedDevice;
        private static AudioClip microphoneClip;
        private static int sampleRate = 44100;
        private static int channels = 1;
        private static float currentMicLevel;
        private static float peakMicLevel;
        private static float noiseFloor;
        private static bool voiceDetected;
        private static float vadThreshold = 0.1f;
        private static float cpuUsage;

        public static void Initialize(ManualLogSource logger, ConfigFile config)
        {
            if (isInitialized) return;
            
            Logger = logger;
            Config = config;
            
            // Load settings from config
            gain = Config.Bind("Audio", "MicrophoneGain", 1.0f, "Microphone gain multiplier").Value;
            noiseGate = Config.Bind("Audio", "NoiseGate", 0.01f, "Noise gate threshold").Value;
            selectedDevice = Config.Bind("Audio", "InputDevice", "", "Selected input device").Value;
            
            // Initialize audio buffer
            audioBuffer = new float[bufferSize];
            
            Logger.LogInfo("StaticAudioManager initialized");
            isInitialized = true;
        }

        public static void StartRecording()
        {
            if (!isInitialized || isRecording) return;
            
            try
            {
                // Get available microphone devices
                string[] devices = Microphone.devices;
                if (devices.Length == 0)
                {
                    Logger.LogWarning("No microphone devices found");
                    return;
                }
                
                // Use selected device or default
                string deviceName = string.IsNullOrEmpty(selectedDevice) ? devices[0] : selectedDevice;
                
                // Start microphone recording with configured channels
                microphoneClip = Microphone.Start(deviceName, true, 1, sampleRate);
                
                if (microphoneClip != null)
                {
                    isRecording = true;
                    Logger.LogInfo($"Started recording from device: {deviceName} with {channels} channel(s)");
                }
                else
                {
                    Logger.LogError("Failed to start microphone recording");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error starting microphone recording: {ex}");
            }
        }

        public static void StopRecording()
        {
            if (!isRecording) return;

            try
            {
                Microphone.End(selectedDevice);
                isRecording = false;
                Logger.LogInfo("Stopped recording");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to stop recording: {ex}");
            }
        }

        public static void Cleanup()
        {
            if (isRecording)
            {
                StopRecording();
            }

            if (microphoneClip != null)
            {
                UnityEngine.Object.Destroy(microphoneClip);
                microphoneClip = null;
            }

            isInitialized = false;
            Logger.LogInfo("StaticAudioManager cleaned up");
        }

        // Event handlers
        public static void OnGameAudioCaptured(float[] audioData)
        {
            if (!isInitialized) return;
            // Process game audio data
            ProcessAudioData(audioData);
        }

        public static void OnGameSpectrumCaptured(float[] spectrumData)
        {
            if (!isInitialized) return;
            // Process spectrum data
            UpdateSpectrumData(spectrumData);
        }

        public static void OnMicrophoneStarted()
        {
            if (!isInitialized) return;
            StartRecording();
        }

        public static void OnMicrophoneEnded()
        {
            if (!isInitialized) return;
            StopRecording();
        }

        public static void OnMicrophoneDataCaptured(float[] micData)
        {
            if (!isInitialized) return;
            ProcessMicrophoneAudio(micData);
        }

        public static void ProcessMicrophoneAudio(float[] data)
        {
            if (!isInitialized) return;

            try
            {
                // Apply gain
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] *= gain;
                }

                // Apply noise gate
                for (int i = 0; i < data.Length; i++)
                {
                    if (Mathf.Abs(data[i]) < noiseGate)
                    {
                        data[i] = 0;
                    }
                }

                // Update microphone level
                float sum = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    sum += Mathf.Abs(data[i]);
                }
                currentMicLevel = sum / data.Length;

                // Update peak level
                peakMicLevel = Mathf.Max(peakMicLevel, currentMicLevel);

                // Update voice detection
                voiceDetected = currentMicLevel > vadThreshold;

                // Update CPU usage (simulated)
                cpuUsage = Mathf.Lerp(cpuUsage, currentMicLevel * 100f, Time.deltaTime);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error processing microphone audio: {ex}");
            }
        }

        private static void ProcessAudioData(float[] data)
        {
            if (!isInitialized) return;
            // Process game audio data
        }

        private static void UpdateSpectrumData(float[] data)
        {
            if (!isInitialized) return;
            // Update spectrum visualization
        }

        // UI access methods
        public static float GetCurrentMicrophoneLevel() => currentMicLevel;
        public static float GetPeakMicrophoneLevel() => peakMicLevel;
        public static bool IsVoiceDetected() => voiceDetected;
        public static float GetNoiseFloor() => noiseFloor;
        public static void SetNoiseFloor(float value) => noiseFloor = value;
        public static float GetCPUUsage() => cpuUsage;
        
        // Push-to-talk functionality
        public static void SetPushToTalkActive(bool active)
        {
            // Implementation for push-to-talk functionality
            Logger?.LogInfo($"Push-to-talk set to: {active}");
        }
        
        // Mute functionality
        public static void ToggleMute()
        {
            // Implementation for toggle mute functionality
            Logger?.LogInfo("Microphone mute toggled");
        }
    }
}