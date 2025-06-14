using System;
using System.Collections.Generic;
using UnityEngine;
using SIPSorcery.Media;
using Concentus.Structs;
using Concentus.Enums;
using RNNoiseSharp;
using System.Numerics;
using System.Threading;
using System.Collections.Concurrent;

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
        private readonly IntPtr _rnnoise;
        private readonly float[] _floatBuffer;
        private readonly short[] _shortBuffer;
        private readonly byte[] _opusBuffer;
        private bool _disposed;

        public WebRTCAudioProcessor(WebRTCAudioProcessingOptions options)
        {
            _options = options;
            _encoder = OpusEncoder.Create(options.SampleRate, options.Channels, OpusApplication.OPUS_APPLICATION_VOIP);
            _decoder = OpusDecoder.Create(options.SampleRate, options.Channels);
            // TODO: Re-enable when RNNoiseSharp is properly configured
            // _rnnoise = options.NoiseSuppression ? RNNoise.rnnoise_create(IntPtr.Zero) : IntPtr.Zero;
            _rnnoise = IntPtr.Zero;
            _floatBuffer = new float[options.FrameSize * options.Channels];
            _shortBuffer = new short[options.FrameSize * options.Channels];
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
                // RNNoise expects exactly 480 samples (10ms at 48kHz)
                int frameSize = 480;
                for (int i = 0; i < length; i += frameSize)
                {
                    int currentFrameSize = Math.Min(frameSize, length - i);
                    if (currentFrameSize == frameSize)
                    {
                        float[] frame = new float[frameSize];
                        Array.Copy(_floatBuffer, i, frame, 0, frameSize);
                        // TODO: Re-enable when RNNoiseSharp is properly configured
                        // RNNoise.rnnoise_process_frame(_rnnoise, frame, frame);
                        Array.Copy(frame, 0, _floatBuffer, i, frameSize);
                    }
                }
            }

            // AGC (simple digital gain, can be replaced with a more advanced algorithm)
            if (_options.AutomaticGainControl)
            {
                float max = 0f;
                for (int i = 0; i < length; i++)
                    max = Math.Max(max, Math.Abs(_floatBuffer[i]));
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

            // Encode with Opus (if speech)
            if (isSpeech)
            {
                for (int i = 0; i < length; i++)
                    _shortBuffer[i] = (short)Math.Clamp(_floatBuffer[i] * short.MaxValue, short.MinValue, short.MaxValue);
                int encoded = _encoder.Encode(_shortBuffer, 0, length, _opusBuffer, 0, _opusBuffer.Length);
                // Optionally, you can decode back to float PCM for further processing or output
                int decoded = _decoder.Decode(_opusBuffer, 0, encoded, _shortBuffer, 0, length, false);
                for (int i = 0; i < length; i++)
                    data[offset + i] = _shortBuffer[i] / (float)short.MaxValue;
            }
            else
            {
                // Silence (mute output)
                Array.Clear(data, offset, length);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            // Note: OpusEncoder and OpusDecoder don't implement IDisposable in Concentus
            // _encoder?.Dispose();
            // _decoder?.Dispose();
            if (_rnnoise != IntPtr.Zero)
            {
                // TODO: Re-enable when RNNoiseSharp is properly configured
                // RNNoise.rnnoise_destroy(_rnnoise);
            }
            _disposed = true;
        }
    }
}