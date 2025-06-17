using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LethalMic
{
    public class AdvancedEchoCanceller : IDisposable
    {
        private readonly int _sampleRate;
        private readonly int _frameSize;
        private readonly int _filterLength;
        private readonly float _stepSize;
        private readonly float _regularization;
        
        // Adaptive filter coefficients
        private readonly float[] _adaptiveFilter;
        private readonly float[] _referenceBuffer;
        private readonly float[] _errorBuffer;
        
        // Circular buffers for delay compensation
        private readonly CircularBuffer _microphoneBuffer;
        private readonly CircularBuffer _speakerBuffer;
        private readonly int _maxDelay;
        
        // Echo path modeling
        private readonly float[] _echoPathEstimate;
        private float _echoPathStrength;
        private readonly float _echoPathAdaptationRate = 0.01f;
        
        // Non-linear processor for residual echo suppression
        private readonly float _nlpThreshold;
        private readonly float _nlpAttenuation;
        private float _nlpGain;
        private readonly float _nlpSmoothingCoeff;
        
        // Double-talk detection
        private readonly DoubleTalkDetector _doubleTalkDetector;
        private bool _isDoubleTalk;
        
        // Frequency domain processing
        private readonly Complex[] _fftBuffer;
        private readonly Complex[] _referenceFFT;
        private readonly Complex[] _microphoneFFT;
        private readonly float[] _powerSpectralDensity;
        private readonly float[] _coherenceFunction;
        private readonly int _fftSize;
        
        // Performance optimization
        private readonly float[] _windowFunction;
        private int _frameCounter;
        private readonly int _adaptationInterval = 4; // Adapt every 4th frame
        
        private bool _disposed = false;
        
        public float EchoCancellationStrength { get; set; } = 1.0f;
        public bool IsEnabled { get; set; } = true;
        
        public AdvancedEchoCanceller(int sampleRate, int frameSize, int filterLength = 512, 
                                   float stepSize = 0.01f, int maxDelay = 1024)
        {
            _sampleRate = sampleRate;
            _frameSize = frameSize;
            _filterLength = filterLength;
            _stepSize = stepSize;
            _regularization = 1e-6f;
            _maxDelay = maxDelay;
            _fftSize = NextPowerOfTwo(System.Math.Max(frameSize, filterLength) * 2);
            
            // Initialize adaptive filter
            _adaptiveFilter = new float[_filterLength];
            _referenceBuffer = new float[_filterLength];
            _errorBuffer = new float[_frameSize];
            _echoPathEstimate = new float[_filterLength];
            
            // Initialize circular buffers
            _microphoneBuffer = new CircularBuffer(_maxDelay + _frameSize);
            _speakerBuffer = new CircularBuffer(_maxDelay + _frameSize);
            
            // Non-linear processor settings
            _nlpThreshold = 0.1f;
            _nlpAttenuation = 0.3f;
            _nlpGain = 1.0f;
            _nlpSmoothingCoeff = 0.95f;
            
            // Initialize double-talk detector
            _doubleTalkDetector = new DoubleTalkDetector(sampleRate, frameSize);
            
            // Initialize frequency domain buffers
            _fftBuffer = new Complex[_fftSize];
            _referenceFFT = new Complex[_fftSize];
            _microphoneFFT = new Complex[_fftSize];
            _powerSpectralDensity = new float[_fftSize / 2 + 1];
            _coherenceFunction = new float[_fftSize / 2 + 1];
            
            // Create window function
            _windowFunction = CreateHannWindow(_frameSize);
            
            _frameCounter = 0;
        }
        
        public float[] ProcessAudio(float[] microphoneInput, float[] speakerReference)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AdvancedEchoCanceller));
            
            if (!IsEnabled || microphoneInput == null || speakerReference == null)
                return microphoneInput ?? new float[0];
            
            int length = System.Math.Min(microphoneInput.Length, speakerReference.Length);
            float[] output = new float[length];
            
            // Store input in circular buffers
            _microphoneBuffer.Write(microphoneInput);
            _speakerBuffer.Write(speakerReference);
            
            // Detect double-talk
            _isDoubleTalk = _doubleTalkDetector.DetectDoubleTalk(microphoneInput, speakerReference);
            
            // Process in blocks
            for (int i = 0; i < length; i += _frameSize)
            {
                int blockSize = System.Math.Min(_frameSize, length - i);
                
                // Extract blocks
                float[] micBlock = new float[blockSize];
                float[] refBlock = new float[blockSize];
                
                Array.Copy(microphoneInput, i, micBlock, 0, blockSize);
                Array.Copy(speakerReference, i, refBlock, 0, blockSize);
                
                // Process block
                float[] processedBlock = ProcessBlock(micBlock, refBlock);
                
                // Copy to output
                Array.Copy(processedBlock, 0, output, i, blockSize);
            }
            
            _frameCounter++;
            return output;
        }
        
        private float[] ProcessBlock(float[] microphoneBlock, float[] referenceBlock)
        {
            // Step 1: Adaptive filtering
            float[] echoEstimate = EstimateEcho(referenceBlock);
            
            // Step 2: Echo subtraction
            float[] errorSignal = new float[microphoneBlock.Length];
            for (int i = 0; i < microphoneBlock.Length; i++)
            {
                errorSignal[i] = microphoneBlock[i] - echoEstimate[i] * EchoCancellationStrength;
            }
            
            // Step 3: Adaptive filter update (only if not double-talk)
            if (!_isDoubleTalk && _frameCounter % _adaptationInterval == 0)
            {
                UpdateAdaptiveFilter(referenceBlock, errorSignal);
            }
            
            // Step 4: Non-linear processing for residual echo suppression
            float[] output = ApplyNonLinearProcessing(errorSignal, microphoneBlock, referenceBlock);
            
            // Step 5: Frequency domain post-processing
            output = ApplyFrequencyDomainProcessing(output, referenceBlock);
            
            return output;
        }
        
        private float[] EstimateEcho(float[] referenceBlock)
        {
            // Update reference buffer
            int refLength = System.Math.Min(referenceBlock.Length, _referenceBuffer.Length);
            Array.Copy(_referenceBuffer, refLength, _referenceBuffer, 0, _referenceBuffer.Length - refLength);
            Array.Copy(referenceBlock, 0, _referenceBuffer, _referenceBuffer.Length - refLength, refLength);
            
            // Convolve with adaptive filter
            float[] echoEstimate = new float[referenceBlock.Length];
            
            for (int i = 0; i < referenceBlock.Length; i++)
            {
                float sum = 0f;
                for (int j = 0; j < _filterLength && i + j < _referenceBuffer.Length; j++)
                {
                    sum += _adaptiveFilter[j] * _referenceBuffer[_referenceBuffer.Length - 1 - i - j];
                }
                echoEstimate[i] = sum;
            }
            
            return echoEstimate;
        }
        
        private void UpdateAdaptiveFilter(float[] referenceBlock, float[] errorSignal)
        {
            // Normalized Least Mean Squares (NLMS) algorithm
            float referenceEnergy = 0f;
            for (int i = 0; i < _referenceBuffer.Length; i++)
            {
                referenceEnergy += _referenceBuffer[i] * _referenceBuffer[i];
            }
            referenceEnergy += _regularization;
            
            float normalizedStepSize = _stepSize / referenceEnergy;
            
            for (int i = 0; i < System.Math.Min(errorSignal.Length, referenceBlock.Length); i++)
            {
                float error = errorSignal[i];
                
                for (int j = 0; j < _filterLength; j++)
                {
                    int refIndex = _referenceBuffer.Length - 1 - i - j;
                    if (refIndex >= 0 && refIndex < _referenceBuffer.Length)
                    {
                        _adaptiveFilter[j] += normalizedStepSize * error * _referenceBuffer[refIndex];
                    }
                }
            }
            
            // Apply filter coefficient constraints
            ApplyFilterConstraints();
        }
        
        private void ApplyFilterConstraints()
        {
            // Prevent filter coefficients from becoming too large
            float maxCoeff = 2.0f;
            for (int i = 0; i < _adaptiveFilter.Length; i++)
            {
                _adaptiveFilter[i] = Mathf.Clamp(_adaptiveFilter[i], -maxCoeff, maxCoeff);
            }
        }
        
        private float[] ApplyNonLinearProcessing(float[] errorSignal, float[] microphoneSignal, float[] referenceSignal)
        {
            float[] output = new float[errorSignal.Length];
            
            // Calculate signal energies
            float errorEnergy = CalculateEnergy(errorSignal);
            float micEnergy = CalculateEnergy(microphoneSignal);
            float refEnergy = CalculateEnergy(referenceSignal);
            
            // Estimate residual echo level
            float echoLevel = EstimateResidualEchoLevel(errorEnergy, refEnergy);
            
            // Calculate suppression gain
            float suppressionGain = 1.0f;
            if (echoLevel > _nlpThreshold && refEnergy > 0.001f)
            {
                float suppressionFactor = Mathf.Clamp01(echoLevel / _nlpThreshold);
                suppressionGain = Mathf.Lerp(1.0f, _nlpAttenuation, suppressionFactor);
            }
            
            // Smooth gain changes
            _nlpGain = _nlpGain * _nlpSmoothingCoeff + suppressionGain * (1f - _nlpSmoothingCoeff);
            
            // Apply suppression
            for (int i = 0; i < errorSignal.Length; i++)
            {
                output[i] = errorSignal[i] * _nlpGain;
            }
            
            return output;
        }
        
        private float EstimateResidualEchoLevel(float errorEnergy, float referenceEnergy)
        {
            if (referenceEnergy < 0.001f) return 0f;
            
            // Simple echo level estimation based on energy ratio
            float echoRatio = errorEnergy / (referenceEnergy + 0.001f);
            
            // Update echo path strength estimate
            _echoPathStrength = _echoPathStrength * (1f - _echoPathAdaptationRate) + 
                               echoRatio * _echoPathAdaptationRate;
            
            return _echoPathStrength;
        }
        
        private float[] ApplyFrequencyDomainProcessing(float[] signal, float[] reference)
        {
            if (signal.Length < _frameSize) return signal;
            
            // Apply window and prepare for FFT
            for (int i = 0; i < _frameSize; i++)
            {
                float micSample = i < signal.Length ? signal[i] * _windowFunction[i] : 0f;
                float refSample = i < reference.Length ? reference[i] * _windowFunction[i] : 0f;
                
                _microphoneFFT[i] = new Complex(micSample, 0);
                _referenceFFT[i] = new Complex(refSample, 0);
            }
            
            // Zero-pad for FFT
            for (int i = _frameSize; i < _fftSize; i++)
            {
                _microphoneFFT[i] = Complex.Zero;
                _referenceFFT[i] = Complex.Zero;
            }
            
            // Perform FFT
            SimpleFFT(_microphoneFFT);
            SimpleFFT(_referenceFFT);
            
            // Calculate coherence and apply spectral suppression
            ApplySpectralSuppression(_microphoneFFT, _referenceFFT);
            
            // Inverse FFT
            SimpleIFFT(_microphoneFFT);
            
            // Extract real part and apply window
            float[] output = new float[signal.Length];
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = (float)_microphoneFFT[i].Real * _windowFunction[i];
            }
            
            return output;
        }
        
        private void ApplySpectralSuppression(Complex[] microphoneSpectrum, Complex[] referenceSpectrum)
        {
            int numBins = _fftSize / 2 + 1;
            
            for (int i = 0; i < numBins && i < microphoneSpectrum.Length; i++)
            {
                float micMagnitude = (float)microphoneSpectrum[i].Magnitude;
                float refMagnitude = (float)referenceSpectrum[i].Magnitude;
                
                // Calculate coherence-based suppression
                float coherence = 0f;
                if (refMagnitude > 0.001f)
                {
                    coherence = (micMagnitude * refMagnitude) / (refMagnitude * refMagnitude + 0.001f);
                }
                
                // Apply frequency-dependent suppression
                float suppressionGain = 1f - coherence * EchoCancellationStrength;
                suppressionGain = Mathf.Clamp(suppressionGain, 0.1f, 1f); // Minimum 10% gain
                
                microphoneSpectrum[i] *= suppressionGain;
            }
        }
        
        private float CalculateEnergy(float[] signal)
        {
            float energy = 0f;
            for (int i = 0; i < signal.Length; i++)
            {
                energy += signal[i] * signal[i];
            }
            return energy / signal.Length;
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
        
        private static int NextPowerOfTwo(int value)
        {
            int power = 1;
            while (power < value)
                power <<= 1;
            return power;
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
        
        public void Reset()
        {
            Array.Clear(_adaptiveFilter, 0, _adaptiveFilter.Length);
            Array.Clear(_referenceBuffer, 0, _referenceBuffer.Length);
            _microphoneBuffer.Clear();
            _speakerBuffer.Clear();
            _echoPathStrength = 0f;
            _nlpGain = 1f;
            _doubleTalkDetector?.Reset();
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            
            _doubleTalkDetector?.Dispose();
            _disposed = true;
        }
    }
    
    // Helper class for double-talk detection
    public class DoubleTalkDetector : IDisposable
    {
        private readonly float[] _micEnergyHistory;
        private readonly float[] _refEnergyHistory;
        private readonly int _historySize = 10;
        private int _historyIndex;
        private bool _disposed = false;
        
        public DoubleTalkDetector(int sampleRate, int frameSize)
        {
            _micEnergyHistory = new float[_historySize];
            _refEnergyHistory = new float[_historySize];
        }
        
        public bool DetectDoubleTalk(float[] microphoneSignal, float[] referenceSignal)
        {
            if (_disposed) return false;
            
            float micEnergy = CalculateEnergy(microphoneSignal);
            float refEnergy = CalculateEnergy(referenceSignal);
            
            _micEnergyHistory[_historyIndex] = micEnergy;
            _refEnergyHistory[_historyIndex] = refEnergy;
            _historyIndex = (_historyIndex + 1) % _historySize;
            
            // Simple double-talk detection based on energy ratios
            float avgMicEnergy = _micEnergyHistory.Average();
            float avgRefEnergy = _refEnergyHistory.Average();
            
            if (avgRefEnergy < 0.001f) return false;
            
            float energyRatio = avgMicEnergy / avgRefEnergy;
            return energyRatio > 0.5f; // Threshold for double-talk detection
        }
        
        private float CalculateEnergy(float[] signal)
        {
            float energy = 0f;
            for (int i = 0; i < signal.Length; i++)
            {
                energy += signal[i] * signal[i];
            }
            return energy / signal.Length;
        }
        
        public void Reset()
        {
            Array.Clear(_micEnergyHistory, 0, _micEnergyHistory.Length);
            Array.Clear(_refEnergyHistory, 0, _refEnergyHistory.Length);
            _historyIndex = 0;
        }
        
        public void Dispose()
        {
            _disposed = true;
        }
    }
    

}