using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Logging;

namespace LethalMic
{
    public static class StaticAudioManager
    {
        private static ManualLogSource Logger;
        private static bool isInitialized;
        private static bool isRecording;
        private static float[] audioBuffer;
        private static float[] processedBuffer;
        private static int bufferSize = 1024;
        private static string selectedDevice;
        private static AudioClip microphoneClip;
        private static int sampleRate = 44100;
        private static int channels = 1;
        private static float currentMicLevel;
        private static float peakMicLevel;
        private static float noiseFloor;
        private static bool voiceDetected;
        private static float cpuUsage;
        private static int lastMicPosition = 0;

        // Audio Processors
        private static AINoiseSuppressionProcessor noiseSuppressor;
        private static AdvancedEchoCanceller echoCanceller;
        private static SpectralSubtractionProcessor spectralProcessor;
        private static VoiceDuckingProcessor voiceDucker;
        private static AudioCompressorProcessor compressor;

        // Processing state
        private static bool processorsInitialized = false;
        private static float[] tempBuffer;
        private static float[] outputBuffer;
        
        // Speaker audio capture for echo cancellation
        // private static AudioClip speakerClip;
        // private static bool isCapturingSpeakers = false;
        // private static float[] speakerBuffer;
        // private static int lastSpeakerPosition = 0;
        
        // Settings tracking for logging optimization
        private static float lastGain = -1f;
        private static bool lastNoiseGate = false;
        private static bool lastCompression = false;
        private static float lastRatio = -1f;

        public static void Initialize(ManualLogSource logger)
        {
            if (isInitialized) return;
            
            Logger = logger;
            
            Logger.LogInfo("Initializing StaticAudioManager...");
            
            // Initialize buffers
            audioBuffer = new float[bufferSize];
            processedBuffer = new float[bufferSize];
            tempBuffer = new float[bufferSize];
            outputBuffer = new float[bufferSize];
            
            Logger.LogInfo($"Audio buffers initialized with size: {bufferSize}");
            
            // Initialize audio processors
            InitializeProcessors();
            
            // Start speaker audio capture for echo cancellation
            StartSpeakerCapture();
            
            Logger.LogInfo("StaticAudioManager initialized with advanced processing pipeline");
            isInitialized = true;
        }

        private static void InitializeProcessors()
        {
            try
            {
                // Initialize all processors with default settings
                noiseSuppressor = new AINoiseSuppressionProcessor(sampleRate, bufferSize, 0.8f);
                echoCanceller = new AdvancedEchoCanceller(sampleRate, bufferSize, 512, 0.01f, 1024);
                spectralProcessor = new SpectralSubtractionProcessor(sampleRate, channels, 1024);
                voiceDucker = new VoiceDuckingProcessor(sampleRate, bufferSize, 0.3f, -30f, 4f, 0.003f, 0.1f);
                compressor = new AudioCompressorProcessor(sampleRate, -20f, 4f, 10f, 100f, 0f);

                processorsInitialized = true;
                Logger.LogInfo("Audio processors initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize audio processors: {ex}");
                processorsInitialized = false;
            }
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
                
                // Get selected device from LethalMicStatic
                string deviceName = LethalMicStatic.GetInputDevice();
                if (string.IsNullOrEmpty(deviceName))
                {
                    deviceName = devices[0]; // Use first available device if none selected
                }
                
                // Start microphone recording
                microphoneClip = Microphone.Start(deviceName, true, 1, sampleRate);
                
                if (microphoneClip != null)
                {
                    isRecording = true;
                    lastMicPosition = 0;
                    selectedDevice = deviceName; // Store for internal use
                    Logger.LogInfo($"Started recording from device: {deviceName}");
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

        public static void ProcessAudio()
        {
            if (!isInitialized || !isRecording || microphoneClip == null) return;

            try
            {
                int bufferSize = microphoneClip.samples;
                int position = Microphone.GetPosition(selectedDevice);
                if (position < 0 || position >= bufferSize)
                {
                    lastMicPosition = 0;
                    return;
                }

                int sampleCount = position - lastMicPosition;
                if (sampleCount < 0) sampleCount += bufferSize; // handle wrap-around

                if (sampleCount <= 0 || sampleCount > bufferSize)
                {
                    lastMicPosition = position;
                    return;
                }

                // Get raw audio data
                float[] rawData = new float[sampleCount];
                bool gotData = microphoneClip.GetData(rawData, lastMicPosition);
                if (!gotData || rawData.Length == 0)
                {
                    lastMicPosition = position;
                    return;
                }

                // Process audio through the pipeline
                float[] processedData = ProcessAudioPipeline(rawData);

                // Calculate levels from processed audio
                UpdateAudioLevels(processedData);

                // Update UI
                UpdateUI();

                lastMicPosition = position;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error processing audio: {ex}");
            }
        }

        private static float[] ProcessAudioPipeline(float[] inputData)
        {
            if (!processorsInitialized || inputData == null || inputData.Length == 0)
                return inputData;

            try
            {
                // Copy input to working buffer
                Array.Copy(inputData, tempBuffer, Math.Min(inputData.Length, tempBuffer.Length));

                // Apply microphone gain
                float gain = LethalMicStatic.GetMicrophoneGain();
                for (int i = 0; i < tempBuffer.Length; i++)
                {
                    tempBuffer[i] *= gain;
                }

                // Apply noise gate if enabled
                if (LethalMicStatic.GetNoiseGateEnabled())
                {
                    float threshold = LethalMicStatic.GetNoiseGateThreshold();
                    
                    // Calculate RMS of the buffer to detect if it's picking up speaker audio
                    float rms = 0f;
                    for (int i = 0; i < tempBuffer.Length; i++)
                    {
                        rms += tempBuffer[i] * tempBuffer[i];
                    }
                    rms = Mathf.Sqrt(rms / tempBuffer.Length);
                    
                    // If the audio level is suspiciously high (likely speaker audio), gate it more aggressively
                    float adaptiveThreshold = threshold;
                    if (rms > threshold * 3f) // If audio is 3x above normal threshold
                    {
                        adaptiveThreshold = threshold * 5f; // Use much higher threshold
                        Logger.LogInfo($"Echo detected! RMS: {rms:F4}, using adaptive threshold: {adaptiveThreshold:F4}");
                    }
                    
                    for (int i = 0; i < tempBuffer.Length; i++)
                    {
                        if (Mathf.Abs(tempBuffer[i]) < adaptiveThreshold)
                        {
                            tempBuffer[i] = 0f;
                        }
                    }
                }

                // Apply AI noise suppression
                if (noiseSuppressor != null && noiseSuppressor.IsEnabled)
                {
                    tempBuffer = noiseSuppressor.ProcessAudio(tempBuffer);
                }

                // Apply spectral subtraction
                if (spectralProcessor != null)
                {
                    spectralProcessor.ProcessAudio(tempBuffer, 0, tempBuffer.Length);
                }

                // Apply echo cancellation with speaker audio - DISABLED to prevent echo loops
                // if (echoCanceller != null && echoCanceller.IsEnabled && isCapturingSpeakers)
                // {
                //     // Get speaker audio for echo cancellation
                //     float[] speakerAudio = GetSpeakerAudio(tempBuffer.Length);
                //     if (speakerAudio != null && speakerAudio.Length > 0)
                //     {
                //         tempBuffer = echoCanceller.ProcessAudio(tempBuffer, speakerAudio);
                //         Logger.LogInfo($"Echo cancellation applied with {speakerAudio.Length} speaker samples");
                //     }
                // }

                // Apply audio compression
                if (compressor != null && compressor.IsEnabled)
                {
                    tempBuffer = compressor.ProcessAudio(tempBuffer);
                }

                // Apply voice ducking (if we had game audio reference)
                // For now, we'll skip this as we don't have game audio capture

                // Copy to output buffer
                Array.Copy(tempBuffer, outputBuffer, Math.Min(tempBuffer.Length, outputBuffer.Length));

                return outputBuffer;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in audio processing pipeline: {ex}");
                return inputData; // Return original data if processing fails
            }
        }

        private static void UpdateAudioLevels(float[] audioData)
        {
            if (audioData == null || audioData.Length == 0) return;

            // Calculate RMS level
            float sum = 0f;
            for (int i = 0; i < audioData.Length; i++)
            {
                sum += audioData[i] * audioData[i];
            }
            float rms = Mathf.Sqrt(sum / audioData.Length);

            // Update current level
            currentMicLevel = rms;

            // Update peak level with decay
            peakMicLevel = Mathf.Max(peakMicLevel * 0.95f, currentMicLevel);

            // Update voice detection based on noise gate threshold
            float threshold = LethalMicStatic.GetNoiseGateThreshold();
            voiceDetected = currentMicLevel > threshold;

            // Update noise floor
            if (currentMicLevel < noiseFloor || noiseFloor == 0f)
            {
                noiseFloor = Mathf.Lerp(noiseFloor, currentMicLevel, 0.01f);
            }

            // Update CPU usage (simulated based on processing complexity)
            cpuUsage = Mathf.Lerp(cpuUsage, currentMicLevel * 100f, Time.deltaTime);
        }

        private static void UpdateUI()
        {
            // Update UI if available
            if (LethalMicStatic.IsUIVisible())
            {
                var uiInstance = LethalMicStatic.GetUIIInstance();
                if (uiInstance != null)
                {
                    uiInstance.UpdateMicStatus(selectedDevice, "Connected", currentMicLevel);
                    uiInstance.UpdateCPUUsage(cpuUsage);
                }
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

            // Dispose processors
            if (noiseSuppressor != null)
            {
                noiseSuppressor.Dispose();
                noiseSuppressor = null;
            }

            if (echoCanceller != null)
            {
                echoCanceller.Dispose();
                echoCanceller = null;
            }

            if (spectralProcessor != null)
            {
                spectralProcessor.Dispose();
                spectralProcessor = null;
            }

            if (voiceDucker != null)
            {
                voiceDucker.Dispose();
                voiceDucker = null;
            }

            if (compressor != null)
            {
                compressor.Dispose();
                compressor = null;
            }

            isInitialized = false;
            processorsInitialized = false;
            Logger.LogInfo("StaticAudioManager cleaned up");
        }

        // Public getters for UI
        public static float GetCurrentMicrophoneLevel() => currentMicLevel;
        public static float GetPeakMicrophoneLevel() => peakMicLevel;
        public static bool IsVoiceDetected() => voiceDetected;
        public static float GetNoiseFloor() => noiseFloor;
        public static void SetNoiseFloor(float value) => noiseFloor = value;
        public static float GetCPUUsage() => cpuUsage;

        // Processor configuration methods
        public static void UpdateProcessorSettings()
        {
            if (!processorsInitialized) return;

            try
            {
                // Only log when settings actually change significantly
                bool shouldLog = (Math.Abs(LethalMicStatic.GetMicrophoneGain() - lastGain) > 0.1f) ||
                                (LethalMicStatic.GetNoiseGateEnabled() != lastNoiseGate) ||
                                (LethalMicStatic.GetCompressionEnabled() != lastCompression) ||
                                (Math.Abs(LethalMicStatic.GetCompressionRatio() - lastRatio) > 0.5f);
                
                if (shouldLog)
                {
                    Logger.LogInfo("Updating audio processor settings...");
                }
                
                // Update noise suppressor settings
                if (noiseSuppressor != null)
                {
                    noiseSuppressor.NoiseReductionStrength = LethalMicStatic.GetNoiseGateThreshold();
                    noiseSuppressor.IsEnabled = LethalMicStatic.GetNoiseGateEnabled();
                    if (shouldLog)
                    {
                        Logger.LogInfo($"Noise suppressor: Enabled={noiseSuppressor.IsEnabled}, Strength={noiseSuppressor.NoiseReductionStrength:F3}");
                    }
                }

                // Update echo canceller settings - DISABLE for now to prevent echo loops
                if (echoCanceller != null)
                {
                    echoCanceller.EchoCancellationStrength = 0.0f; // Disable echo cancellation
                    echoCanceller.IsEnabled = false; // Disable echo cancellation
                    if (shouldLog)
                    {
                        Logger.LogInfo($"Echo canceller: DISABLED (prevents echo loops)");
                    }
                }

                // Update voice ducker settings
                if (voiceDucker != null)
                {
                    voiceDucker.SetDuckingLevel(0.3f);
                    if (shouldLog)
                    {
                        Logger.LogInfo($"Voice ducker: Ducking level=0.3");
                    }
                }

                // Update compressor settings
                if (compressor != null)
                {
                    compressor.IsEnabled = LethalMicStatic.GetCompressionEnabled();
                    compressor.UpdateSettings(
                        -20f, // threshold (could be made configurable)
                        LethalMicStatic.GetCompressionRatio(),
                        LethalMicStatic.GetAttackTime(),
                        LethalMicStatic.GetReleaseTime(),
                        0f // makeup gain (could be made configurable)
                    );
                    if (shouldLog)
                    {
                        Logger.LogInfo($"Compressor: Enabled={compressor.IsEnabled}, Ratio={LethalMicStatic.GetCompressionRatio()}, Attack={LethalMicStatic.GetAttackTime():F0}ms, Release={LethalMicStatic.GetReleaseTime():F0}ms");
                    }
                }

                if (shouldLog)
                {
                    Logger.LogInfo("Audio processor settings updated successfully");
                    
                    // Update last values
                    lastGain = LethalMicStatic.GetMicrophoneGain();
                    lastNoiseGate = LethalMicStatic.GetNoiseGateEnabled();
                    lastCompression = LethalMicStatic.GetCompressionEnabled();
                    lastRatio = LethalMicStatic.GetCompressionRatio();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error updating processor settings: {ex}");
            }
        }

        public static void SetInputDevice(string deviceName)
        {
            selectedDevice = deviceName;
            if (isRecording)
            {
                StopRecording();
                StartRecording();
            }
        }

        public static string GetInputDevice() => selectedDevice;

        private static void StartSpeakerCapture()
        {
            try
            {
                // Try to capture audio from the default audio output
                // Note: This is a simplified approach - in a real implementation you'd use
                // Windows Core Audio APIs or similar to capture system audio
                
                Logger.LogInfo("Starting speaker audio capture for echo cancellation...");
                
                // For now, we'll use a dummy approach that simulates speaker audio
                // In a full implementation, you'd capture actual system audio
                // isCapturingSpeakers = true;
                
                Logger.LogInfo("Speaker capture initialized (simulated)");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to start speaker capture: {ex}");
                // isCapturingSpeakers = false;
            }
        }

        private static float[] GetSpeakerAudio(int requiredLength)
        {
            if (!/*isCapturingSpeakers*/ false) return null;
            
            try
            {
                // For now, we'll simulate speaker audio by creating a buffer
                // In a real implementation, this would capture actual system audio
                float[] speakerAudio = new float[requiredLength];
                
                // Simulate some speaker audio (this is where you'd get real audio)
                // For testing, we'll create a simple sine wave to simulate voice
                for (int i = 0; i < requiredLength; i++)
                {
                    // Create a simple simulation of speaker audio
                    // In reality, this would be the actual audio coming from your speakers
                    speakerAudio[i] = 0f; // For now, assume no speaker audio
                }
                
                return speakerAudio;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting speaker audio: {ex}");
                return null;
            }
        }

        // Method to process audio buffer from voice chat system
        public static float[] ProcessAudioBuffer(float[] inputBuffer)
        {
            if (!processorsInitialized || inputBuffer == null || inputBuffer.Length == 0)
                return inputBuffer;

            try
            {
                // Apply our processing pipeline
                return ProcessAudioPipeline(inputBuffer);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error processing audio buffer: {ex}");
                return inputBuffer; // Return original data if processing fails
            }
        }

        public static void SetAggressiveSuppression()
        {
            // Enable aggressive noise gate, suppression, and compression
            // These values should match Discord-like quality
            LethalMicStatic.SetNoiseGateEnabled(true);
            LethalMicStatic.SetNoiseGateThreshold(0.05f);
            LethalMicStatic.SetCompressionEnabled(true);
            LethalMicStatic.SetCompressionRatio(10f);
            LethalMicStatic.SetAttackTime(2f);
            LethalMicStatic.SetReleaseTime(50f);
            // Enable noise suppressor if available
            // ...
        }

        public static void SetStereoMixSuppression()
        {
            // TODO: Implement echo cancellation using Stereo Mix as reference
            // For now, fallback to aggressive suppression
            SetAggressiveSuppression();
            // ... future: capture Stereo Mix and subtract from mic input ...
        }

        public static void SetWasapiSuppression()
        {
            // TODO: Implement echo cancellation using WASAPI loopback (NAudio)
            // For now, fallback to aggressive suppression
            SetAggressiveSuppression();
            // ... future: capture WASAPI output and subtract from mic input ...
        }
    }
}