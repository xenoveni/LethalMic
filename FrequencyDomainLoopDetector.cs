using System;
using System.Numerics;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace LethalMic
{
    public class FrequencyDomainLoopDetector : IDisposable
    {
        private readonly int _sampleRate;
        private readonly int _fftSize;
        private readonly int _maxBufferSize;
        private readonly Complex[] _inputFFT;
        private readonly Complex[] _outputFFT;
        private readonly float[] _inputMagnitude;
        private readonly float[] _outputMagnitude;
        private readonly float[] _correlationHistory;
        private readonly float[] _windowFunction;
        private readonly Queue<float[]> _inputHistory;
        private readonly Queue<float[]> _outputHistory;
        private readonly int _historyLength;
        private int _frameCount;
        private bool _disposed = false;
        
        // Frequency band analysis
        private int[] _frequencyBands;
        private readonly float[] _bandCorrelations;
        private readonly float _voiceFreqMin = 300f;   // Hz
        private readonly float _voiceFreqMax = 3400f;  // Hz
        
        // Adaptive thresholding
        private float _adaptiveThreshold;
        private readonly float _thresholdAdaptationRate = 0.05f;
        
        public FrequencyDomainLoopDetector(int sampleRate, int fftSize = 1024, int maxBufferSize = 48000)
        {
            _sampleRate = sampleRate;
            _fftSize = fftSize;
            _maxBufferSize = maxBufferSize;
            _inputFFT = new Complex[_fftSize];
            _outputFFT = new Complex[_fftSize];
            _inputMagnitude = new float[_fftSize / 2 + 1];
            _outputMagnitude = new float[_fftSize / 2 + 1];
            _correlationHistory = new float[10]; // Store last 10 correlation values
            _windowFunction = CreateHannWindow(_fftSize);
            _historyLength = 5; // Keep 5 frames of history
            _inputHistory = new Queue<float[]>(_historyLength);
            _outputHistory = new Queue<float[]>(_historyLength);
            _frameCount = 0;
            _adaptiveThreshold = 0.3f;
            
            // Initialize frequency bands for analysis
            InitializeFrequencyBands();
            _bandCorrelations = new float[_frequencyBands.Length - 1];
        }
        
        private void InitializeFrequencyBands()
        {
            // Define frequency bands for analysis (in Hz)
            List<float> bands = new List<float> { 0, 100, 300, 800, 1500, 3000, 6000, 12000, _sampleRate / 2 };
            _frequencyBands = bands.Select(f => (int)(f * _fftSize / _sampleRate)).ToArray();
        }
        
        public bool DetectLoop(float[] inputData, float[] outputData, float threshold)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FrequencyDomainLoopDetector));
            
            // Store current frame in history
            StoreFrameHistory(inputData, outputData);
            
            if (_inputHistory.Count < 2) return false; // Need at least 2 frames
            
            // Analyze multiple frames for better detection
            float maxCorrelation = 0f;
            bool loopDetected = false;
            
            var inputFrames = _inputHistory.ToArray();
            var outputFrames = _outputHistory.ToArray();
            
            for (int i = 0; i < inputFrames.Length - 1; i++)
            {
                for (int j = 0; j < outputFrames.Length; j++)
                {
                    float correlation = AnalyzeFrequencyCorrelation(inputFrames[i], outputFrames[j]);
                    maxCorrelation = System.Math.Max(maxCorrelation, correlation);
                    
                    // Check if correlation exceeds adaptive threshold
                    if (correlation > _adaptiveThreshold)
                    {
                        // Additional validation: check frequency band correlations
                        if (ValidateLoopInFrequencyBands(inputFrames[i], outputFrames[j]))
                        {
                            loopDetected = true;
                        }
                    }
                }
            }
            
            // Update adaptive threshold
            UpdateAdaptiveThreshold(maxCorrelation);
            
            // Store correlation in history for trend analysis
            UpdateCorrelationHistory(maxCorrelation);
            
            // Additional check: look for sustained correlation patterns
            if (!loopDetected)
            {
                loopDetected = DetectSustainedCorrelation();
            }
            
            return loopDetected;
        }
        
        private void StoreFrameHistory(float[] inputData, float[] outputData)
        {
            // Convert to mono and store
            float[] inputFrame = ConvertToMono(inputData);
            float[] outputFrame = ConvertToMono(outputData);
            
            _inputHistory.Enqueue(inputFrame);
            _outputHistory.Enqueue(outputFrame);
            
            while (_inputHistory.Count > _historyLength)
            {
                _inputHistory.Dequeue();
                _outputHistory.Dequeue();
            }
            
            _frameCount++;
        }
        
        private float[] ConvertToMono(float[] data)
        {
            int frameSize = System.Math.Min(_fftSize, data.Length);
            float[] mono = new float[frameSize];
            
            for (int i = 0; i < frameSize; i++)
            {
                mono[i] = i < data.Length ? data[i] : 0f;
            }
            
            return mono;
        }
        
        private float AnalyzeFrequencyCorrelation(float[] inputFrame, float[] outputFrame)
        {
            // Apply window function and prepare for FFT
            for (int i = 0; i < _fftSize; i++)
            {
                float inputSample = i < inputFrame.Length ? inputFrame[i] * _windowFunction[i] : 0f;
                float outputSample = i < outputFrame.Length ? outputFrame[i] * _windowFunction[i] : 0f;
                
                _inputFFT[i] = new Complex(inputSample, 0);
                _outputFFT[i] = new Complex(outputSample, 0);
            }
            
            // Perform FFT
            SimpleFFT(_inputFFT);
            SimpleFFT(_outputFFT);
            
            // Calculate magnitude spectra
            for (int i = 0; i < _inputMagnitude.Length; i++)
            {
                _inputMagnitude[i] = (float)_inputFFT[i].Magnitude;
                _outputMagnitude[i] = (float)_outputFFT[i].Magnitude;
            }
            
            // Calculate spectral correlation with emphasis on voice frequencies
            return CalculateSpectralCorrelation(_inputMagnitude, _outputMagnitude);
        }
        
        private float CalculateSpectralCorrelation(float[] spectrum1, float[] spectrum2)
        {
            float correlation = 0f;
            float norm1 = 0f;
            float norm2 = 0f;
            int voiceStartBin = (int)(_voiceFreqMin * _fftSize / _sampleRate);
            int voiceEndBin = (int)(_voiceFreqMax * _fftSize / _sampleRate);
            
            // Focus on voice frequency range
            for (int i = voiceStartBin; i < System.Math.Min(voiceEndBin, spectrum1.Length); i++)
            {
                correlation += spectrum1[i] * spectrum2[i];
                norm1 += spectrum1[i] * spectrum1[i];
                norm2 += spectrum2[i] * spectrum2[i];
            }
            
            if (norm1 > 0 && norm2 > 0)
            {
                return correlation / Mathf.Sqrt(norm1 * norm2);
            }
            
            return 0f;
        }
        
        private bool ValidateLoopInFrequencyBands(float[] inputFrame, float[] outputFrame)
        {
            // Analyze correlation in different frequency bands
            AnalyzeFrequencyCorrelation(inputFrame, outputFrame);
            
            for (int band = 0; band < _frequencyBands.Length - 1; band++)
            {
                int startBin = _frequencyBands[band];
                int endBin = _frequencyBands[band + 1];
                
                float bandCorrelation = 0f;
                float norm1 = 0f;
                float norm2 = 0f;
                
                for (int i = startBin; i < System.Math.Min(endBin, _inputMagnitude.Length); i++)
                {
                    bandCorrelation += _inputMagnitude[i] * _outputMagnitude[i];
                    norm1 += _inputMagnitude[i] * _inputMagnitude[i];
                    norm2 += _outputMagnitude[i] * _outputMagnitude[i];
                }
                
                if (norm1 > 0 && norm2 > 0)
                {
                    _bandCorrelations[band] = bandCorrelation / Mathf.Sqrt(norm1 * norm2);
                }
                else
                {
                    _bandCorrelations[band] = 0f;
                }
            }
            
            // Check if multiple bands show high correlation (indicates real loop)
            int highCorrelationBands = 0;
            for (int i = 0; i < _bandCorrelations.Length; i++)
            {
                if (_bandCorrelations[i] > 0.4f) // Lower threshold for individual bands
                {
                    highCorrelationBands++;
                }
            }
            
            return highCorrelationBands >= 2; // At least 2 bands must show correlation
        }
        
        private void UpdateAdaptiveThreshold(float currentCorrelation)
        {
            // Adapt threshold based on recent correlation patterns
            float targetThreshold = 0.3f; // Base threshold
            
            // If consistently low correlation, lower threshold for sensitivity
            float avgRecentCorrelation = _correlationHistory.Where(c => c > 0).DefaultIfEmpty(0).Average();
            if (avgRecentCorrelation < 0.1f)
            {
                targetThreshold = 0.25f;
            }
            else if (avgRecentCorrelation > 0.5f)
            {
                targetThreshold = 0.4f; // Raise threshold if frequently detecting
            }
            
            _adaptiveThreshold = Mathf.Lerp(_adaptiveThreshold, targetThreshold, _thresholdAdaptationRate);
        }
        
        private void UpdateCorrelationHistory(float correlation)
        {
            // Shift history
            for (int i = _correlationHistory.Length - 1; i > 0; i--)
            {
                _correlationHistory[i] = _correlationHistory[i - 1];
            }
            _correlationHistory[0] = correlation;
        }
        
        private bool DetectSustainedCorrelation()
        {
            // Look for sustained correlation patterns that might indicate a loop
            int sustainedCount = 0;
            float sustainedThreshold = _adaptiveThreshold * 0.8f;
            
            for (int i = 0; i < System.Math.Min(5, _correlationHistory.Length); i++)
            {
                if (_correlationHistory[i] > sustainedThreshold)
                {
                    sustainedCount++;
                }
            }
            
            return sustainedCount >= 3; // 3 out of last 5 frames show correlation
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
        
        // Simplified FFT implementation
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
        
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}