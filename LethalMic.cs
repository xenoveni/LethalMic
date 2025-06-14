using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace LethalMic
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.xenoveni.lethalmic";
        public const string PLUGIN_NAME = "LethalMic";
        public const string PLUGIN_VERSION = "1.0.0";
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class LethalMic : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        private new readonly ManualLogSource Logger;
        private ConfigEntry<bool> enableCompression;
        private ConfigEntry<float> compressionThreshold;
        private ConfigEntry<float> compressionRatio;
        private ConfigEntry<float> attackTime;
        private ConfigEntry<float> releaseTime;
        private ConfigEntry<bool> enableNoiseReduction;
        private ConfigEntry<float> noiseReductionStrength;
        private ConfigEntry<int> fftSize;
        private ConfigEntry<bool> enableEchoSuppression;
        private ConfigEntry<float> echoSuppressionStrength;
        private ConfigEntry<bool> enableAdaptiveEQ;
        private ConfigEntry<bool> enableSpatialEnhancement;
        private ConfigEntry<bool> enableLogging;
        private ConfigEntry<bool> enableVAD;
        private ConfigEntry<float> vadThreshold;
        private ConfigEntry<float> vadAttackTime;
        private ConfigEntry<float> vadReleaseTime;
        private ConfigEntry<float> voiceMinFreq;
        private ConfigEntry<float> voiceMaxFreq;
        private ConfigEntry<float> noiseGateThreshold;
        private ConfigEntry<float> noiseGateAttack;
        private ConfigEntry<float> noiseGateRelease;
        private ConfigEntry<bool> enableLoopDetection;
        private ConfigEntry<float> loopDetectionThreshold;
        private ConfigEntry<int> loopDetectionWindow;
        private ConfigEntry<float> loopSuppressionStrength;

        // Audio processing variables
        private float[] noiseProfile;
        private float[] previousFrame;
        private float[] currentFrame;
        private float[] processedFrame;
        private Complex[] fftBuffer;
        private float[] windowFunction;
        private float[] crossCorrelationBuffer;
        private float[] adaptiveEQBuffer;
        private float[] spatialEnhancementBuffer;
        private float[] voiceActivityBuffer;
        private float[] outputBuffer;
        private float[] inputBuffer;
        private float voiceActivityLevel;
        private float noiseGateLevel;
        private int sampleRate;
        private int channelCount;
        private bool isInitialized;
        private readonly object processingLock = new object();
        private readonly Queue<Exception> errorQueue = new Queue<Exception>();
        private const int MAX_ERROR_QUEUE_SIZE = 10;
        private const int MAX_BUFFER_SIZE = 48000; // 1 second at 48kHz

        public LethalMic()
        {
            Logger = BepInEx.Logging.Logger.CreateLogSource(PluginInfo.PLUGIN_NAME);
            
            // Initialize configuration
            enableCompression = Config.Bind("Audio Processing", "Enable Dynamic Range Compression", true, 
                "Enable dynamic range compression to make quiet sounds more audible");
            compressionThreshold = Config.Bind("Audio Processing", "Compression Threshold", -20f, 
                "Threshold in dB for compression to start");
            compressionRatio = Config.Bind("Audio Processing", "Compression Ratio", 4f, 
                "Compression ratio (e.g., 4:1)");
            attackTime = Config.Bind("Audio Processing", "Attack Time", 5f, 
                "Attack time in milliseconds");
            releaseTime = Config.Bind("Audio Processing", "Release Time", 50f, 
                "Release time in milliseconds");
            enableNoiseReduction = Config.Bind("Audio Processing", "Enable Noise Reduction", true, 
                "Enable noise reduction using FFT-based spectral subtraction");
            noiseReductionStrength = Config.Bind("Audio Processing", "Noise Reduction Strength", 0.7f, 
                "Strength of noise reduction (0-1)");
            fftSize = Config.Bind("Audio Processing", "FFT Size", 2048, 
                "Size of FFT for spectral analysis (power of 2)");
            enableEchoSuppression = Config.Bind("Audio Processing", "Enable Echo Suppression", true, 
                "Enable echo suppression using cross-correlation");
            echoSuppressionStrength = Config.Bind("Audio Processing", "Echo Suppression Strength", 0.8f, 
                "Strength of echo suppression (0-1)");
            enableAdaptiveEQ = Config.Bind("Audio Processing", "Enable Adaptive EQ", true, 
                "Enable adaptive equalization based on frequency content");
            enableSpatialEnhancement = Config.Bind("Audio Processing", "Enable Spatial Enhancement", true, 
                "Enable spatial audio enhancement for better 3D positioning");
            enableLogging = Config.Bind("Debug", "Enable Detailed Logging", false, 
                "Enable detailed logging for troubleshooting");
            enableVAD = Config.Bind("Voice Detection", "Enable Voice Activity Detection", true,
                "Enable voice activity detection to filter out non-speech audio");
            vadThreshold = Config.Bind("Voice Detection", "VAD Threshold", -30f,
                "Threshold in dB for voice activity detection");
            vadAttackTime = Config.Bind("Voice Detection", "VAD Attack Time", 10f,
                "Attack time in milliseconds for voice detection");
            vadReleaseTime = Config.Bind("Voice Detection", "VAD Release Time", 100f,
                "Release time in milliseconds for voice detection");
            voiceMinFreq = Config.Bind("Voice Detection", "Minimum Voice Frequency", 85f,
                "Minimum frequency in Hz for voice detection");
            voiceMaxFreq = Config.Bind("Voice Detection", "Maximum Voice Frequency", 255f,
                "Maximum frequency in Hz for voice detection");
            noiseGateThreshold = Config.Bind("Voice Detection", "Noise Gate Threshold", -45f,
                "Threshold in dB for noise gate");
            noiseGateAttack = Config.Bind("Voice Detection", "Noise Gate Attack Time", 5f,
                "Attack time in milliseconds for noise gate");
            noiseGateRelease = Config.Bind("Voice Detection", "Noise Gate Release Time", 50f,
                "Release time in milliseconds for noise gate");
            enableLoopDetection = Config.Bind("Audio Loop", "Enable Loop Detection", true,
                "Enable detection and prevention of audio feedback loops");
            loopDetectionThreshold = Config.Bind("Audio Loop", "Loop Detection Threshold", 0.7f,
                "Threshold for detecting audio feedback loops (0-1)");
            loopDetectionWindow = Config.Bind("Audio Loop", "Loop Detection Window", 1000,
                "Time window in milliseconds for loop detection");
            loopSuppressionStrength = Config.Bind("Audio Loop", "Loop Suppression Strength", 0.9f,
                "Strength of loop suppression (0-1)");

            // Subscribe to config change events
            enableCompression.SettingChanged += (s, e) => ReinitializeProcessing();
            enableNoiseReduction.SettingChanged += (s, e) => ReinitializeProcessing();
            enableEchoSuppression.SettingChanged += (s, e) => ReinitializeProcessing();
            enableAdaptiveEQ.SettingChanged += (s, e) => ReinitializeProcessing();
            enableSpatialEnhancement.SettingChanged += (s, e) => ReinitializeProcessing();
            fftSize.SettingChanged += (s, e) => ReinitializeProcessing();
            enableVAD.SettingChanged += (s, e) => ReinitializeProcessing();
            vadThreshold.SettingChanged += (s, e) => ReinitializeProcessing();
            voiceMinFreq.SettingChanged += (s, e) => ReinitializeProcessing();
            voiceMaxFreq.SettingChanged += (s, e) => ReinitializeProcessing();
            noiseGateThreshold.SettingChanged += (s, e) => ReinitializeProcessing();
            enableLoopDetection.SettingChanged += (s, e) => ReinitializeProcessing();
            loopDetectionThreshold.SettingChanged += (s, e) => ReinitializeProcessing();
            loopDetectionWindow.SettingChanged += (s, e) => ReinitializeProcessing();
            loopSuppressionStrength.SettingChanged += (s, e) => ReinitializeProcessing();
        }

        private void Awake()
        {
            try
            {
                harmony.PatchAll();
                Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize plugin: {ex}");
                throw;
            }
        }

        private void OnDestroy()
        {
            try
            {
                harmony.UnpatchSelf();
                Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is unloaded!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error during plugin cleanup: {ex}");
            }
        }

        private void ReinitializeProcessing()
        {
            try
            {
                lock (processingLock)
                {
                    isInitialized = false;
                    InitializeAudioProcessing();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to reinitialize audio processing: {ex}");
                AddErrorToQueue(ex);
            }
        }

        private void InitializeAudioProcessing()
        {
            try
            {
                if (!isInitialized)
                {
                    // Get audio settings from Unity
                    sampleRate = AudioSettings.outputSampleRate;
                    channelCount = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;

                    // Initialize buffers
                    int bufferSize = fftSize.Value;
                    noiseProfile = new float[bufferSize];
                    previousFrame = new float[bufferSize];
                    currentFrame = new float[bufferSize];
                    processedFrame = new float[bufferSize];
                    fftBuffer = new Complex[bufferSize];
                    windowFunction = new float[bufferSize];
                    crossCorrelationBuffer = new float[bufferSize];
                    adaptiveEQBuffer = new float[bufferSize];
                    spatialEnhancementBuffer = new float[bufferSize];
                    voiceActivityBuffer = new float[bufferSize];
                    
                    // Initialize loop detection buffers
                    int maxBufferSize = Math.Min(MAX_BUFFER_SIZE, sampleRate * loopDetectionWindow.Value / 1000);
                    outputBuffer = new float[maxBufferSize];
                    inputBuffer = new float[maxBufferSize];

                    // Initialize window function (Hann window)
                    for (int i = 0; i < bufferSize; i++)
                    {
                        windowFunction[i] = 0.5f * (1 - Mathf.Cos(2 * Mathf.PI * i / (bufferSize - 1)));
                    }

                    voiceActivityLevel = 0;
                    noiseGateLevel = 0;

                    isInitialized = true;
                    if (enableLogging.Value)
                    {
                        Logger.LogInfo($"Audio processing initialized with sample rate: {sampleRate}Hz, channels: {channelCount}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize audio processing: {ex}");
                AddErrorToQueue(ex);
                throw;
            }
        }

        private void AddErrorToQueue(Exception ex)
        {
            lock (errorQueue)
            {
                while (errorQueue.Count >= MAX_ERROR_QUEUE_SIZE)
                {
                    errorQueue.Dequeue();
                }
                errorQueue.Enqueue(ex);
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            try
            {
                if (!isInitialized)
                {
                    InitializeAudioProcessing();
                }

                lock (processingLock)
                {
                    // Store output audio for loop detection
                    if (enableLoopDetection.Value)
                    {
                        StoreOutputAudio(data, channels);
                    }

                    ProcessAudio(data, channels);

                    // Check for audio loops
                    if (enableLoopDetection.Value)
                    {
                        DetectAndPreventLoops(data, channels);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in audio processing: {ex}");
                AddErrorToQueue(ex);
                // Fall back to unprocessed audio
                return;
            }
        }

        private void StoreOutputAudio(float[] data, int channels)
        {
            try
            {
                // Shift existing buffer
                Array.Copy(outputBuffer, data.Length / channels, outputBuffer, 0, outputBuffer.Length - data.Length / channels);
                
                // Store new data
                for (int i = 0; i < data.Length / channels; i++)
                {
                    float sample = 0;
                    for (int ch = 0; ch < channels; ch++)
                    {
                        sample += data[i * channels + ch];
                    }
                    outputBuffer[outputBuffer.Length - data.Length / channels + i] = sample / channels;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error storing output audio: {ex}");
                AddErrorToQueue(ex);
            }
        }

        private void DetectAndPreventLoops(float[] data, int channels)
        {
            try
            {
                // Shift input buffer
                Array.Copy(inputBuffer, data.Length / channels, inputBuffer, 0, inputBuffer.Length - data.Length / channels);
                
                // Store current input
                for (int i = 0; i < data.Length / channels; i++)
                {
                    float sample = 0;
                    for (int ch = 0; ch < channels; ch++)
                    {
                        sample += data[i * channels + ch];
                    }
                    inputBuffer[inputBuffer.Length - data.Length / channels + i] = sample / channels;
                }

                // Calculate cross-correlation between input and output
                float maxCorrelation = 0;
                int maxLag = 0;
                int correlationWindow = Math.Min(loopDetectionWindow.Value * sampleRate / 1000, inputBuffer.Length);

                for (int lag = 0; lag < correlationWindow; lag++)
                {
                    float correlation = 0;
                    float norm1 = 0;
                    float norm2 = 0;

                    for (int i = 0; i < correlationWindow - lag; i++)
                    {
                        correlation += inputBuffer[i] * outputBuffer[i + lag];
                        norm1 += inputBuffer[i] * inputBuffer[i];
                        norm2 += outputBuffer[i + lag] * outputBuffer[i + lag];
                    }

                    if (norm1 > 0 && norm2 > 0)
                    {
                        correlation /= Mathf.Sqrt(norm1 * norm2);
                        if (correlation > maxCorrelation)
                        {
                            maxCorrelation = correlation;
                            maxLag = lag;
                        }
                    }
                }

                // If we detect a strong correlation, suppress the loop
                if (maxCorrelation > loopDetectionThreshold.Value)
                {
                    float suppressionFactor = loopSuppressionStrength.Value * maxCorrelation;
                    
                    // Apply suppression to the current frame
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] *= (1 - suppressionFactor);
                    }

                    if (enableLogging.Value)
                    {
                        Logger.LogInfo($"Detected audio loop with correlation {maxCorrelation:F3} at lag {maxLag}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in loop detection: {ex}");
                AddErrorToQueue(ex);
            }
        }

        private void ProcessAudio(float[] data, int channels)
        {
            try
            {
                int samplesPerChannel = data.Length / channels;
                int hopSize = fftSize.Value / 4; // 75% overlap

                for (int i = 0; i < samplesPerChannel; i += hopSize)
                {
                    // Copy current frame
                    Array.Copy(data, i * channels, currentFrame, 0, Math.Min(fftSize.Value, samplesPerChannel - i));

                    // Apply window function
                    for (int j = 0; j < fftSize.Value; j++)
                    {
                        currentFrame[j] *= windowFunction[j];
                    }

                    // Perform FFT
                    FFT(currentFrame, fftBuffer, true);

                    if (enableVAD.Value)
                    {
                        // Detect voice activity
                        bool isVoice = DetectVoiceActivity(fftBuffer);
                        
                        if (!isVoice)
                        {
                            // If no voice detected, apply strong noise gate
                            ApplyNoiseGate(fftBuffer);
                        }
                        else
                        {
                            // Apply voice-specific processing
                            ApplyVoiceProcessing(fftBuffer);
                        }
                    }

                    if (enableNoiseReduction.Value)
                    {
                        // Estimate and update noise floor
                        EstimateNoiseFloor(fftBuffer);
                        
                        // Apply spectral subtraction
                        ApplySpectralSubtraction(fftBuffer);
                    }

                    if (enableEchoSuppression.Value)
                    {
                        // Perform cross-correlation for echo detection
                        PerformCrossCorrelation(currentFrame, previousFrame);
                        
                        // Apply echo suppression
                        ApplyEchoSuppression(fftBuffer);
                    }

                    if (enableAdaptiveEQ.Value)
                    {
                        // Apply adaptive equalization
                        ApplyAdaptiveEQ(fftBuffer);
                    }

                    if (enableSpatialEnhancement.Value)
                    {
                        // Apply spatial enhancement
                        ApplySpatialEnhancement(fftBuffer, channels);
                    }

                    // Perform inverse FFT
                    FFT(fftBuffer, processedFrame, false);

                    // Apply overlap-add
                    for (int j = 0; j < fftSize.Value; j++)
                    {
                        if (i + j < samplesPerChannel)
                        {
                            data[(i + j) * channels] = processedFrame[j];
                            if (channels > 1)
                            {
                                data[(i + j) * channels + 1] = processedFrame[j];
                            }
                        }
                    }

                    // Store current frame for next iteration
                    Array.Copy(currentFrame, previousFrame, fftSize.Value);
                }

                if (enableCompression.Value)
                {
                    // Apply dynamic range compression
                    ApplyCompression(data, channels);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in audio processing: {ex}");
                AddErrorToQueue(ex);
                throw;
            }
        }

        private void FFT(float[] input, Complex[] output, bool forward)
        {
            try
            {
                int n = input.Length;
                if (n == 0 || (n & (n - 1)) != 0)
                {
                    throw new ArgumentException("Input length must be a power of 2");
                }

                // Copy input to output
                for (int i = 0; i < n; i++)
                {
                    output[i] = new Complex(input[i], 0);
                }

                // Bit reversal
                for (int i = 0; i < n; i++)
                {
                    int j = ReverseBits(i, n);
                    if (j > i)
                    {
                        Complex temp = output[i];
                        output[i] = output[j];
                        output[j] = temp;
                    }
                }

                // FFT computation
                for (int size = 2; size <= n; size *= 2)
                {
                    double angle = (forward ? -2 : 2) * Math.PI / size;
                    Complex w = new Complex(Math.Cos(angle), Math.Sin(angle));

                    for (int i = 0; i < n; i += size)
                    {
                        Complex wj = new Complex(1, 0);
                        for (int j = 0; j < size / 2; j++)
                        {
                            Complex u = output[i + j];
                            Complex t = wj * output[i + j + size / 2];
                            output[i + j] = u + t;
                            output[i + j + size / 2] = u - t;
                            wj *= w;
                        }
                    }
                }

                // Normalize for inverse FFT
                if (!forward)
                {
                    for (int i = 0; i < n; i++)
                    {
                        output[i] /= n;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in FFT computation: {ex}");
                AddErrorToQueue(ex);
                throw;
            }
        }

        private void FFT(Complex[] input, float[] output, bool inverse)
        {
            try
            {
                int n = input.Length;
                if (n == 0 || (n & (n - 1)) != 0)
                {
                    throw new ArgumentException("Input length must be a power of 2");
                }

                // Copy input to output
                Complex[] temp = new Complex[n];
                Array.Copy(input, temp, n);

                // Bit reversal
                for (int i = 0; i < n; i++)
                {
                    int j = ReverseBits(i, n);
                    if (j > i)
                    {
                        Complex t = temp[i];
                        temp[i] = temp[j];
                        temp[j] = t;
                    }
                }

                // FFT computation
                for (int size = 2; size <= n; size *= 2)
                {
                    double angle = (inverse ? 2 : -2) * Math.PI / size;
                    Complex w = new Complex(Math.Cos(angle), Math.Sin(angle));

                    for (int i = 0; i < n; i += size)
                    {
                        Complex wj = new Complex(1, 0);
                        for (int j = 0; j < size / 2; j++)
                        {
                            Complex u = temp[i + j];
                            Complex t = wj * temp[i + j + size / 2];
                            temp[i + j] = u + t;
                            temp[i + j + size / 2] = u - t;
                            wj *= w;
                        }
                    }
                }

                // Copy result to output
                for (int i = 0; i < n; i++)
                {
                    output[i] = (float)temp[i].Real;
                }

                // Normalize for inverse FFT
                if (inverse)
                {
                    for (int i = 0; i < n; i++)
                    {
                        output[i] /= n;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in FFT computation: {ex}");
                AddErrorToQueue(ex);
                throw;
            }
        }

        private int ReverseBits(int x, int n)
        {
            int result = 0;
            int bits = (int)Math.Log(n, 2);
            for (int i = 0; i < bits; i++)
            {
                result = (result << 1) | (x & 1);
                x >>= 1;
            }
            return result;
        }

        private void EstimateNoiseFloor(Complex[] spectrum)
        {
            try
            {
                // Use minimum statistics method for noise floor estimation
                float alpha = 0.95f; // Smoothing factor
                for (int i = 0; i < spectrum.Length; i++)
                {
                    float magnitude = (float)spectrum[i].Magnitude;
                    noiseProfile[i] = Mathf.Min(noiseProfile[i], magnitude);
                    noiseProfile[i] = alpha * noiseProfile[i] + (1 - alpha) * magnitude;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in noise floor estimation: {ex}");
                AddErrorToQueue(ex);
                throw;
            }
        }

        private void ApplySpectralSubtraction(Complex[] spectrum)
        {
            try
            {
                float beta = noiseReductionStrength.Value;
                for (int i = 0; i < spectrum.Length; i++)
                {
                    float magnitude = (float)spectrum[i].Magnitude;
                    float phase = (float)spectrum[i].Phase;
                    float subtractedMagnitude = Mathf.Max(0, magnitude - beta * noiseProfile[i]);
                    spectrum[i] = Complex.FromPolarCoordinates(subtractedMagnitude, phase);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in spectral subtraction: {ex}");
                AddErrorToQueue(ex);
                throw;
            }
        }

        private void PerformCrossCorrelation(float[] current, float[] previous)
        {
            try
            {
                int n = current.Length;
                for (int lag = 0; lag < n; lag++)
                {
                    float sum = 0;
                    for (int i = 0; i < n - lag; i++)
                    {
                        sum += current[i] * previous[i + lag];
                    }
                    crossCorrelationBuffer[lag] = sum;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in cross-correlation: {ex}");
                AddErrorToQueue(ex);
                throw;
            }
        }

        private void ApplyEchoSuppression(Complex[] spectrum)
        {
            try
            {
                // Find echo delay using cross-correlation
                int maxLag = 0;
                float maxCorrelation = 0;
                for (int i = 1; i < crossCorrelationBuffer.Length; i++)
                {
                    if (crossCorrelationBuffer[i] > maxCorrelation)
                    {
                        maxCorrelation = crossCorrelationBuffer[i];
                        maxLag = i;
                    }
                }

                if (maxCorrelation > 0.1f) // Threshold for echo detection
                {
                    float suppressionFactor = echoSuppressionStrength.Value * maxCorrelation;
                    for (int i = 0; i < spectrum.Length; i++)
                    {
                        float magnitude = (float)spectrum[i].Magnitude;
                        float phase = (float)spectrum[i].Phase;
                        float suppressedMagnitude = magnitude * (1 - suppressionFactor);
                        spectrum[i] = Complex.FromPolarCoordinates(suppressedMagnitude, phase);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in echo suppression: {ex}");
                AddErrorToQueue(ex);
                throw;
            }
        }

        private void ApplyAdaptiveEQ(Complex[] spectrum)
        {
            try
            {
                // Simple adaptive EQ based on frequency content
                float[] frequencyBands = new float[] { 60, 170, 310, 600, 1000, 3000, 6000, 12000, 14000, 16000 };
                float[] gains = new float[frequencyBands.Length];

                // Calculate gains for each band
                for (int i = 0; i < frequencyBands.Length - 1; i++)
                {
                    float lowFreq = frequencyBands[i];
                    float highFreq = frequencyBands[i + 1];
                    float bandEnergy = 0;
                    int count = 0;

                    for (int j = 0; j < spectrum.Length; j++)
                    {
                        float freq = j * sampleRate / (float)spectrum.Length;
                        if (freq >= lowFreq && freq < highFreq)
                        {
                            bandEnergy += (float)spectrum[j].Magnitude;
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        bandEnergy /= count;
                        gains[i] = Mathf.Clamp(1 / (bandEnergy + 0.001f), 0.5f, 2f);
                    }
                }

                // Apply gains
                for (int i = 0; i < spectrum.Length; i++)
                {
                    float freq = i * sampleRate / (float)spectrum.Length;
                    float gain = 1f;

                    for (int j = 0; j < frequencyBands.Length - 1; j++)
                    {
                        if (freq >= frequencyBands[j] && freq < frequencyBands[j + 1])
                        {
                            gain = gains[j];
                            break;
                        }
                    }

                    float magnitude = (float)spectrum[i].Magnitude;
                    float phase = (float)spectrum[i].Phase;
                    spectrum[i] = Complex.FromPolarCoordinates(magnitude * gain, phase);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in adaptive EQ: {ex}");
                AddErrorToQueue(ex);
                throw;
            }
        }

        private void ApplySpatialEnhancement(Complex[] spectrum, int channels)
        {
            try
            {
                if (channels > 1)
                {
                    // Apply HRTF-like processing for spatial enhancement
                    float[] frequencies = new float[spectrum.Length];
                    for (int i = 0; i < spectrum.Length; i++)
                    {
                        frequencies[i] = i * sampleRate / (float)spectrum.Length;
                    }

                    // Simple HRTF approximation
                    for (int i = 0; i < spectrum.Length; i++)
                    {
                        float freq = frequencies[i];
                        float magnitude = (float)spectrum[i].Magnitude;
                        float phase = (float)spectrum[i].Phase;

                        // Frequency-dependent phase shift
                        float phaseShift = Mathf.Sin(freq * 0.0001f) * 0.1f;
                        spectrum[i] = Complex.FromPolarCoordinates(magnitude, phase + phaseShift);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in spatial enhancement: {ex}");
                AddErrorToQueue(ex);
                throw;
            }
        }

        private void ApplyCompression(float[] data, int channels)
        {
            try
            {
                float threshold = Mathf.Pow(10, compressionThreshold.Value / 20);
                float ratio = compressionRatio.Value;
                float attack = attackTime.Value / 1000f;
                float release = releaseTime.Value / 1000f;
                float attackCoeff = Mathf.Exp(-1 / (attack * sampleRate));
                float releaseCoeff = Mathf.Exp(-1 / (release * sampleRate));
                float envelope = 0;
                float gain = 1;

                for (int i = 0; i < data.Length; i += channels)
                {
                    // Calculate envelope
                    float input = Mathf.Abs(data[i]);
                    envelope = Mathf.Max(input, envelope * (input > envelope ? attackCoeff : releaseCoeff));

                    // Calculate gain
                    if (envelope > threshold)
                    {
                        float compression = (envelope - threshold) / ratio + threshold;
                        gain = compression / envelope;
                    }

                    // Apply gain
                    for (int j = 0; j < channels; j++)
                    {
                        data[i + j] *= gain;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in compression: {ex}");
                AddErrorToQueue(ex);
                throw;
            }
        }

        private bool DetectVoiceActivity(Complex[] spectrum)
        {
            try
            {
                float voiceEnergy = 0;
                float totalEnergy = 0;
                int voiceBandCount = 0;
                int totalBandCount = 0;

                for (int i = 0; i < spectrum.Length; i++)
                {
                    float freq = i * sampleRate / (float)spectrum.Length;
                    float magnitude = (float)spectrum[i].Magnitude;

                    if (freq >= voiceMinFreq.Value && freq <= voiceMaxFreq.Value)
                    {
                        voiceEnergy += magnitude;
                        voiceBandCount++;
                    }
                    totalEnergy += magnitude;
                    totalBandCount++;
                }

                if (voiceBandCount > 0 && totalBandCount > 0)
                {
                    float voiceRatio = voiceEnergy / voiceBandCount;
                    float totalRatio = totalEnergy / totalBandCount;
                    
                    // Update voice activity level with attack/release
                    float targetLevel = voiceRatio > totalRatio * 1.5f ? 1.0f : 0.0f;
                    float attackCoeff = Mathf.Exp(-1 / (vadAttackTime.Value / 1000f * sampleRate));
                    float releaseCoeff = Mathf.Exp(-1 / (vadReleaseTime.Value / 1000f * sampleRate));
                    
                    voiceActivityLevel = targetLevel > voiceActivityLevel ?
                        targetLevel * (1 - attackCoeff) + voiceActivityLevel * attackCoeff :
                        targetLevel * (1 - releaseCoeff) + voiceActivityLevel * releaseCoeff;

                    return voiceActivityLevel > Mathf.Pow(10, vadThreshold.Value / 20);
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in voice activity detection: {ex}");
                AddErrorToQueue(ex);
                return false;
            }
        }

        private void ApplyNoiseGate(Complex[] spectrum)
        {
            try
            {
                float threshold = Mathf.Pow(10, noiseGateThreshold.Value / 20);
                float attackCoeff = Mathf.Exp(-1 / (noiseGateAttack.Value / 1000f * sampleRate));
                float releaseCoeff = Mathf.Exp(-1 / (noiseGateRelease.Value / 1000f * sampleRate));

                for (int i = 0; i < spectrum.Length; i++)
                {
                    float magnitude = (float)spectrum[i].Magnitude;
                    float phase = (float)spectrum[i].Phase;

                    // Update noise gate level
                    float targetLevel = magnitude > threshold ? 1.0f : 0.0f;
                    noiseGateLevel = targetLevel > noiseGateLevel ?
                        targetLevel * (1 - attackCoeff) + noiseGateLevel * attackCoeff :
                        targetLevel * (1 - releaseCoeff) + noiseGateLevel * releaseCoeff;

                    // Apply noise gate
                    spectrum[i] = Complex.FromPolarCoordinates(magnitude * noiseGateLevel, phase);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in noise gate application: {ex}");
                AddErrorToQueue(ex);
                throw;
            }
        }

        private void ApplyVoiceProcessing(Complex[] spectrum)
        {
            try
            {
                // Apply bandpass filter for voice frequencies
                for (int i = 0; i < spectrum.Length; i++)
                {
                    float freq = i * sampleRate / (float)spectrum.Length;
                    float magnitude = (float)spectrum[i].Magnitude;
                    float phase = (float)spectrum[i].Phase;

                    // Apply frequency-dependent gain
                    float gain = 1.0f;
                    if (freq < voiceMinFreq.Value || freq > voiceMaxFreq.Value)
                    {
                        gain = 0.1f; // Strongly attenuate frequencies outside voice range
                    }
                    else
                    {
                        // Boost voice frequencies slightly
                        gain = 1.2f;
                    }

                    spectrum[i] = Complex.FromPolarCoordinates(magnitude * gain, phase);
                }

                // Apply enhanced echo suppression
                if (enableEchoSuppression.Value)
                {
                    ApplyEchoSuppression(spectrum);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in voice processing: {ex}");
                AddErrorToQueue(ex);
                throw;
            }
        }
    }
} 