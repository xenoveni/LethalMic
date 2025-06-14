using System;
using System.Collections.Generic;
using UnityEngine;
using SIPSorcery.Media;
using Concentus.Structs;
using Concentus.Enums;
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
        private readonly OpusEncoder _encoder;
        private readonly OpusDecoder _decoder;
        private IntPtr _rnnoise;
        private readonly float[] _floatBuffer;
        private readonly short[] _shortBuffer;
        private readonly short[] _rnnoiseBuffer;
        private readonly byte[] _opusBuffer;
        private bool _disposed;
        
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
            
            try
            {
                _encoder = OpusEncoder.Create(options.SampleRate, options.Channels, OpusApplication.OPUS_APPLICATION_VOIP);
                _decoder = OpusDecoder.Create(options.SampleRate, options.Channels);
                
                // Configure Opus encoder for better voice quality
                _encoder.Bitrate = 32000; // 32 kbps for good voice quality
                _encoder.Complexity = options.ProcessingQuality;
                _encoder.UseVBR = true;
                _encoder.UseDTX = true; // Discontinuous transmission for silence
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to initialize Opus codec: {ex.Message}");
                throw;
            }
            
            // Initialize RNNoise if available
            try
            {
                if (options.NoiseSuppression)
                {
                    _rnnoise = rnnoise_create(IntPtr.Zero);
                    if (_rnnoise == IntPtr.Zero)
                    {
                        UnityEngine.Debug.LogWarning("RNNoise initialization failed, noise suppression disabled");
                    }
                }
            }
            catch (DllNotFoundException)
            {
                UnityEngine.Debug.LogWarning("RNNoise library not found, noise suppression disabled");
                _rnnoise = IntPtr.Zero;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"RNNoise initialization failed: {ex.Message}");
                _rnnoise = IntPtr.Zero;
            }
            
            _floatBuffer = new float[options.FrameSize * options.Channels];
            _shortBuffer = new short[options.FrameSize * options.Channels];
            _rnnoiseBuffer = new short[480]; // RNNoise frame size (10ms at 48kHz)
            _opusBuffer = new byte[4000]; // Max Opus packet size
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

            // VAD (energy-based, can be replaced with Opus VAD or more advanced)
            bool isSpeech = true;
            if (_options.VoiceActivityDetection)
            {
                float energy = 0f;
                for (int i = 0; i < length; i++)
                    energy += _floatBuffer[i] * _floatBuffer[i];
                energy /= length;
                isSpeech = energy > 0.001f;
            }

            // Encode with Opus (if speech) for quality enhancement
            if (isSpeech)
            {
                try
                {
                    // Convert float to short for Opus
                    for (int i = 0; i < length; i++)
                        _shortBuffer[i] = (short)System.Math.Clamp(_floatBuffer[i] * short.MaxValue, short.MinValue, short.MaxValue);
                    
                    // Encode with Opus
                    int encoded = _encoder.Encode(_shortBuffer, 0, length, _opusBuffer, 0, _opusBuffer.Length);
                    
                    if (encoded > 0)
                    {
                        // Decode back to get Opus-processed audio
                        int decoded = _decoder.Decode(_opusBuffer, 0, encoded, _shortBuffer, 0, length, false);
                        
                        if (decoded > 0)
                        {
                            // Convert back to float
                            for (int i = 0; i < System.Math.Min(decoded, length); i++)
                                data[offset + i] = _shortBuffer[i] / (float)short.MaxValue;
                        }
                        else
                        {
                            // Fallback to original audio if decode fails
                            for (int i = 0; i < length; i++)
                                data[offset + i] = _floatBuffer[i];
                        }
                    }
                    else
                    {
                        // Fallback to original audio if encode fails
                        for (int i = 0; i < length; i++)
                            data[offset + i] = _floatBuffer[i];
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"Opus processing failed: {ex.Message}");
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
            
            // Note: OpusEncoder and OpusDecoder in Concentus don't implement IDisposable
            // They are managed objects and will be garbage collected
            
            _disposed = true;
        }
    }
}