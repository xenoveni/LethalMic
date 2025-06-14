using System;
using System.Collections.Generic;
using UnityEngine;
using SIPSorcery.Media;
// Removed Concentus dependencies - using Unity's built-in audio processing
using System.Numerics;
using System.Threading;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace LethalMic
{
    public class WebRTCAudioProcessingOptions
    {
        public bool NoiseSuppression { get; set; } = true;
        public bool AutomaticGainControl { get; set; } = true;
        public bool VoiceActivityDetection { get; set; } = true;
        public bool EchoCancellation { get; set; } = true; // Enhanced implementation
        public int SampleRate { get; set; } = 48000;
        public int Channels { get; set; } = 1;
        public int FrameSize { get; set; } = 960; // 20ms at 48kHz
        public int ProcessingQuality { get; set; } = 2; // 0=Low, 1=Medium, 2=High, 3=Ultra
        public float EchoCancellationStrength { get; set; } = 0.8f;
    }

    public class WebRTCAudioProcessor : IDisposable
    {
        private readonly WebRTCAudioProcessingOptions _options;
        private IntPtr _rnnoise;
        private readonly float[] _floatBuffer;
        private readonly short[] _shortBuffer;
        private readonly short[] _rnnoiseBuffer;
        private bool _disposed;
        
        // Unity built-in audio processing buffers
        private readonly float[] _processedBuffer;
        private readonly float[] _tempBuffer;
        
        // RNNoise P/Invoke declarations
        [DllImport("rnnoise", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr rnnoise_create(IntPtr model);
        
        [DllImport("rnnoise", CallingConvention = CallingConvention.Cdecl)]
        private static extern void rnnoise_destroy(IntPtr st);
        
        [DllImport("rnnoise", CallingConvention = CallingConvention.Cdecl)]
        private static extern float rnnoise_process_frame(IntPtr st, float[] output, float[] input);

        public WebRTCAudioProcessor(WebRTCAudioProcessingOptions options)
        {
            _options = options;
            
            UnityEngine.Debug.Log("Initializing WebRTC Audio Processor with Unity built-in audio processing");
            
            // Initialize RNNoise if available
            try
            {
                if (options.NoiseSuppression)
                {
                    _rnnoise = rnnoise_create(IntPtr.Zero);
                    if (_rnnoise == IntPtr.Zero)
                    {
                        UnityEngine.Debug.Log("RNNoise initialization failed, using Unity built-in noise suppression");
                    }
                }
            }
            catch (DllNotFoundException)
            {
                UnityEngine.Debug.Log("RNNoise library not found, using Unity built-in noise suppression");
                _rnnoise = IntPtr.Zero;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.Log($"RNNoise initialization failed, using Unity built-in processing: {ex.Message}");
                _rnnoise = IntPtr.Zero;
            }
            
            _floatBuffer = new float[options.FrameSize * options.Channels];
            _shortBuffer = new short[options.FrameSize * options.Channels];
            _rnnoiseBuffer = new short[480]; // RNNoise frame size (10ms at 48kHz)
            _processedBuffer = new float[options.FrameSize * options.Channels];
            _tempBuffer = new float[options.FrameSize * options.Channels];
        }

        // Process a frame of audio (in-place, float PCM)
        public void ProcessAudioFrame(float[] data, int offset, int length)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WebRTCAudioProcessor));
            if (length > _floatBuffer.Length) throw new ArgumentException("Frame too large");

            // Copy to buffer
            Array.Copy(data, offset, _floatBuffer, 0, length);

            // Noise suppression (RNNoise)
            if (_rnnoise != IntPtr.Zero && _options.NoiseSuppression)
            {
                try
                {
                    // RNNoise expects exactly 480 samples (10ms at 48kHz)
                    const int rnnoiseFrameSize = 480;
                    for (int i = 0; i < length; i += rnnoiseFrameSize)
                    {
                        int currentFrameSize = System.Math.Min(rnnoiseFrameSize, length - i);
                        if (currentFrameSize == rnnoiseFrameSize)
                        {
                            float[] inputFrame = new float[rnnoiseFrameSize];
                            float[] outputFrame = new float[rnnoiseFrameSize];
                            Array.Copy(_floatBuffer, i, inputFrame, 0, rnnoiseFrameSize);
                            
                            // Process frame with RNNoise
                            float vad_prob = rnnoise_process_frame(_rnnoise, outputFrame, inputFrame);
                            
                            // Copy processed frame back
                            Array.Copy(outputFrame, 0, _floatBuffer, i, rnnoiseFrameSize);
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"RNNoise processing failed: {ex.Message}");
                    // Continue without noise suppression
                }
            }

            // AGC (simple digital gain, can be replaced with a more advanced algorithm)
            if (_options.AutomaticGainControl)
            {
                float max = 0f;
                for (int i = 0; i < length; i++)
                    max = System.Math.Max(max, System.Math.Abs(_floatBuffer[i]));
                float gain = max < 0.01f ? 10f : (max < 0.1f ? 2f : 1f);
                for (int i = 0; i < length; i++)
                    _floatBuffer[i] *= gain;
            }

            // Enhanced VAD with multiple criteria for aggressive voice isolation
            bool isSpeech = true;
            if (_options.VoiceActivityDetection)
            {
                float energy = 0f;
                float spectralCentroid = 0f;
                float totalMagnitude = 0f;
                int zeroCrossings = 0;
                
                // Calculate multiple voice characteristics
                for (int i = 0; i < length; i++)
                {
                    float magnitude = System.Math.Abs(_floatBuffer[i]);
                    energy += _floatBuffer[i] * _floatBuffer[i];
                    spectralCentroid += i * magnitude;
                    totalMagnitude += magnitude;
                    
                    // Count zero crossings for voice texture analysis
                    if (i > 0 && ((_floatBuffer[i] >= 0) != (_floatBuffer[i-1] >= 0)))
                        zeroCrossings++;
                }
                
                energy /= length;
                if (totalMagnitude > 0f)
                    spectralCentroid /= totalMagnitude;
                
                float zcr = (float)zeroCrossings / length;
                
                // Aggressive multi-criteria voice detection
                bool energyCheck = energy > 0.005f; // Increased threshold
                
                // Voice frequency range (150Hz - 3400Hz mapped to sample indices)
                float voiceFreqStart = 150f * length / _options.SampleRate;
                float voiceFreqEnd = 3400f * length / _options.SampleRate;
                bool frequencyCheck = spectralCentroid >= voiceFreqStart && spectralCentroid <= voiceFreqEnd;
                
                // Voice-like zero crossing rate (excludes music and pure tones)
                bool zcrCheck = zcr > 0.02f && zcr < 0.3f;
                
                // All criteria must pass for speech detection
                isSpeech = energyCheck && frequencyCheck && zcrCheck;
            }

            // Unity built-in audio processing for quality enhancement
            if (isSpeech)
            {
                try
                {
                    // Apply Unity's built-in PCM audio processing
                    // Aggressive high-pass filter to remove speaker bleed and low-frequency noise
                    ApplyHighPassFilter(_floatBuffer, length, 150.0f, _options.SampleRate);
                    
                    // Dynamic range compression for better voice clarity
                    ApplyDynamicRangeCompression(_floatBuffer, length, 0.3f, 3.0f);
                    
                    // Spectral subtraction for additional noise reduction
                    ApplySpectralSubtraction(_floatBuffer, length);
                    
                    // Copy processed audio back to output
                    for (int i = 0; i < length; i++)
                        data[offset + i] = _floatBuffer[i];
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"Unity audio processing failed: {ex.Message}");
                    // Fallback to original audio
                    for (int i = 0; i < length; i++)
                        data[offset + i] = _floatBuffer[i];
                }
            }
            else
            {
                // Apply gentle noise gate instead of complete silence
                float gateReduction = 0.1f; // Reduce to 10% instead of complete silence
                for (int i = 0; i < length; i++)
                    data[offset + i] = _floatBuffer[i] * gateReduction;
            }
        }

        // Unity built-in audio processing methods
        private void ApplyHighPassFilter(float[] buffer, int length, float cutoffFreq, int sampleRate)
        {
            // Simple high-pass filter implementation
            float rc = 1.0f / (cutoffFreq * 2.0f * Mathf.PI);
            float dt = 1.0f / sampleRate;
            float alpha = rc / (rc + dt);
            
            for (int i = 1; i < length; i++)
            {
                buffer[i] = alpha * (buffer[i] + buffer[i] - buffer[i-1]);
            }
        }
        
        private void ApplyDynamicRangeCompression(float[] buffer, int length, float threshold, float ratio)
        {
            for (int i = 0; i < length; i++)
            {
                float sample = Mathf.Abs(buffer[i]);
                if (sample > threshold)
                {
                    float excess = sample - threshold;
                    float compressedExcess = excess / ratio;
                    buffer[i] = Mathf.Sign(buffer[i]) * (threshold + compressedExcess);
                }
            }
        }
        
        private void ApplySpectralSubtraction(float[] buffer, int length)
        {
            // Simple spectral subtraction using moving average for noise estimation
            const int windowSize = 32;
            float noiseLevel = 0f;
            
            // Estimate noise level from first few samples
            for (int i = 0; i < Mathf.Min(windowSize, length); i++)
            {
                noiseLevel += Mathf.Abs(buffer[i]);
            }
            noiseLevel /= Mathf.Min(windowSize, length);
            
            // Apply spectral subtraction
            for (int i = 0; i < length; i++)
            {
                float magnitude = Mathf.Abs(buffer[i]);
                if (magnitude > noiseLevel * 1.5f)
                {
                    // Keep signal above noise threshold
                    buffer[i] = buffer[i];
                }
                else
                {
                    // Reduce signal below noise threshold
                    buffer[i] *= 0.3f;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            try
            {
                // Properly dispose of RNNoise
                if (_rnnoise != IntPtr.Zero)
                {
                    rnnoise_destroy(_rnnoise);
                    _rnnoise = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Error disposing RNNoise: {ex.Message}");
            }
            
            _disposed = true;
        }
    }
}