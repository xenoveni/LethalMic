using System;
using System.Numerics;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace LethalMic
{
    public class SpectralSubtractionProcessor : IDisposable
    {
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _fftSize;
        private readonly float[] _noiseSpectrum;
        private readonly Complex[] _fftBuffer;
        private readonly float[] _magnitudeBuffer;
        private readonly float[] _phaseBuffer;
        private readonly float[] _windowFunction;
        private readonly float _alpha = 2.0f; // Over-subtraction factor
        private readonly float _beta = 0.01f; // Spectral floor factor
        private bool _noiseEstimated = false;
        private int _frameCount = 0;
        private bool _disposed = false;

        public SpectralSubtractionProcessor(int sampleRate, int channels, int fftSize = 1024)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            _fftSize = fftSize;
            _noiseSpectrum = new float[_fftSize / 2 + 1];
            _fftBuffer = new Complex[_fftSize];
            _magnitudeBuffer = new float[_fftSize / 2 + 1];
            _phaseBuffer = new float[_fftSize / 2 + 1];
            _windowFunction = CreateHannWindow(_fftSize);
        }

        public void ProcessAudio(float[] data, int offset, int length)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SpectralSubtractionProcessor));
            
            // Process in overlapping frames
            int hopSize = _fftSize / 4;
            for (int i = 0; i < length - _fftSize; i += hopSize)
            {
                ProcessFrame(data, offset + i);
            }
        }

        private void ProcessFrame(float[] data, int offset)
        {
            // Apply window function and copy to FFT buffer
            for (int i = 0; i < _fftSize; i++)
            {
                if (offset + i < data.Length)
                {
                    _fftBuffer[i] = new Complex(data[offset + i] * _windowFunction[i], 0);
                }
                else
                {
                    _fftBuffer[i] = Complex.Zero;
                }
            }

            // Perform FFT (simplified - in real implementation use a proper FFT library)
            SimpleFFT(_fftBuffer);

            // Calculate magnitude and phase
            for (int i = 0; i < _fftSize / 2 + 1; i++)
            {
                _magnitudeBuffer[i] = (float)_fftBuffer[i].Magnitude;
                _phaseBuffer[i] = (float)System.Math.Atan2(_fftBuffer[i].Imaginary, _fftBuffer[i].Real);
            }

            // Estimate noise spectrum from first few frames
            if (!_noiseEstimated && _frameCount < 10)
            {
                for (int i = 0; i < _magnitudeBuffer.Length; i++)
                {
                    _noiseSpectrum[i] = (_noiseSpectrum[i] * _frameCount + _magnitudeBuffer[i]) / (_frameCount + 1);
                }
                _frameCount++;
                if (_frameCount >= 10)
                {
                    _noiseEstimated = true;
                }
            }

            // Apply spectral subtraction
            if (_noiseEstimated)
            {
                for (int i = 0; i < _magnitudeBuffer.Length; i++)
                {
                    float subtractedMagnitude = _magnitudeBuffer[i] - _alpha * _noiseSpectrum[i];
                    float spectralFloor = _beta * _magnitudeBuffer[i];
                    _magnitudeBuffer[i] = System.Math.Max(subtractedMagnitude, spectralFloor);
                }
            }

            // Reconstruct complex spectrum
            for (int i = 0; i < _fftSize / 2 + 1; i++)
            {
                _fftBuffer[i] = Complex.FromPolarCoordinates(_magnitudeBuffer[i], _phaseBuffer[i]);
                if (i > 0 && i < _fftSize / 2)
                {
                    _fftBuffer[_fftSize - i] = Complex.Conjugate(_fftBuffer[i]);
                }
            }

            // Perform IFFT
            SimpleIFFT(_fftBuffer);

            // Apply window and overlap-add back to signal
            for (int i = 0; i < _fftSize && offset + i < data.Length; i++)
            {
                data[offset + i] = (float)_fftBuffer[i].Real * _windowFunction[i] * 0.5f;
            }
        }

        private float[] CreateHannWindow(int size)
        {
            float[] window = new float[size];
            for (int i = 0; i < size; i++)
            {
                window[i] = 0.5f * (1.0f - Mathf.Cos(2.0f * Mathf.PI * i / (size - 1)));
            }
            return window;
        }

        // Simplified FFT implementation (for demonstration - use a proper FFT library in production)
        private void SimpleFFT(Complex[] buffer)
        {
            int n = buffer.Length;
            if (n <= 1) return;

            // Bit-reverse permutation
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1)
                {
                    j ^= bit;
                }
                j ^= bit;
                if (i < j)
                {
                    (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
                }
            }

            // Cooley-Tukey FFT
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = 2 * System.Math.PI / len;
                Complex wlen = new Complex(System.Math.Cos(ang), System.Math.Sin(ang));
                for (int i = 0; i < n; i += len)
                {
                    Complex w = Complex.One;
                    for (int j = 0; j < len / 2; j++)
                    {
                        Complex u = buffer[i + j];
                        Complex v = buffer[i + j + len / 2] * w;
                        buffer[i + j] = u + v;
                        buffer[i + j + len / 2] = u - v;
                        w *= wlen;
                    }
                }
            }
        }

        private void SimpleIFFT(Complex[] buffer)
        {
            // Conjugate
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = Complex.Conjugate(buffer[i]);
            }

            // FFT
            SimpleFFT(buffer);

            // Conjugate and scale
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = Complex.Conjugate(buffer[i]) / buffer.Length;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}