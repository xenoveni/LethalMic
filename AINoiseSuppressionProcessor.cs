using System;
using System.Numerics;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LethalMic
{
    public class AINoiseSuppressionProcessor : IDisposable
    {
        private readonly int _sampleRate;
        private readonly int _frameSize;
        private readonly int _fftSize;
        private readonly float _noiseReductionStrength;
        
        // Neural network simulation (simplified)
        private readonly NeuralNoiseReducer _neuralReducer;
        
        // Spectral processing
        private readonly Complex[] _fftBuffer;
        private readonly Complex[] _noiseProfile;
        private readonly float[] _magnitudeSpectrum;
        private readonly float[] _phaseSpectrum;
        private readonly float[] _noiseMagnitude;
        private readonly float[] _cleanMagnitude;
        private readonly float[] _windowFunction;
        
        // Adaptive noise estimation
        private readonly float[] _noiseFloorEstimate;
        private readonly float[] _signalToNoiseRatio;
        private readonly float _noiseAdaptationRate = 0.01f;
        private readonly float _signalAdaptationRate = 0.1f;
        
        // Multi-band processing
        private readonly int _numBands = 8;
        private float[][] _bandFilters;
        private float[] _bandGains;
        private float[] _bandNoiseFloors;
        
        // Voice activity detection for noise learning
        private readonly VoiceActivityDetector _vadForNoise;
        private bool _isLearningNoise;
        private int _noiseLearnFrames;
        private readonly int _maxNoiseLearnFrames = 100; // Learn noise for first 100 frames
        
        // Advanced features
        private readonly SpectralSubtractor _spectralSubtractor;
        private readonly WienerFilter _wienerFilter;
        private readonly ResidualNoiseReducer _residualReducer;
        
        // Performance optimization
        private readonly float[] _overlapBuffer;
        private readonly int _hopSize;
        private int _frameCounter;
        
        private bool _disposed = false;
        
        public float NoiseReductionStrength { get; set; } = 1.0f;
        public bool IsEnabled { get; set; } = true;
        public bool IsLearningNoise => _isLearningNoise;
        
        public AINoiseSuppressionProcessor(int sampleRate, int frameSize, float noiseReductionStrength = 0.8f)
        {
            _sampleRate = sampleRate;
            _frameSize = frameSize;
            _fftSize = NextPowerOfTwo(frameSize * 2);
            _noiseReductionStrength = Mathf.Clamp01(noiseReductionStrength);
            _hopSize = frameSize / 2; // 50% overlap
            
            // Initialize buffers
            _fftBuffer = new Complex[_fftSize];
            _noiseProfile = new Complex[_fftSize];
            _magnitudeSpectrum = new float[_fftSize / 2 + 1];
            _phaseSpectrum = new float[_fftSize / 2 + 1];
            _noiseMagnitude = new float[_fftSize / 2 + 1];
            _cleanMagnitude = new float[_fftSize / 2 + 1];
            _noiseFloorEstimate = new float[_fftSize / 2 + 1];
            _signalToNoiseRatio = new float[_fftSize / 2 + 1];
            _overlapBuffer = new float[_frameSize];
            
            // Initialize window function
            _windowFunction = CreateHannWindow(_frameSize);
            
            // Initialize multi-band processing
            InitializeMultiBandProcessing();
            
            // Initialize advanced processors
            _neuralReducer = new NeuralNoiseReducer(sampleRate, _fftSize / 2 + 1);
            _vadForNoise = new VoiceActivityDetector(sampleRate, frameSize);
            _spectralSubtractor = new SpectralSubtractor(_fftSize / 2 + 1);
            _wienerFilter = new WienerFilter(_fftSize / 2 + 1);
            _residualReducer = new ResidualNoiseReducer(_fftSize / 2 + 1);
            
            // Initialize noise learning
            _isLearningNoise = true;
            _noiseLearnFrames = 0;
            
            // Initialize noise floor with small values
            for (int i = 0; i < _noiseFloorEstimate.Length; i++)
            {
                _noiseFloorEstimate[i] = 0.001f;
            }
        }
        
        private void InitializeMultiBandProcessing()
        {
            _bandFilters = new float[_numBands][];
            _bandGains = new float[_numBands];
            _bandNoiseFloors = new float[_numBands];
            
            // Create frequency bands (logarithmic spacing)
            for (int band = 0; band < _numBands; band++)
            {
                _bandFilters[band] = new float[_fftSize / 2 + 1];
                _bandGains[band] = 1.0f;
                _bandNoiseFloors[band] = 0.001f;
                
                // Calculate band boundaries
                float startFreq = (float)(20 * System.Math.Pow(2, band * 10.0 / _numBands)); // 20 Hz to ~20 kHz
                float endFreq = (float)(20 * System.Math.Pow(2, (band + 1) * 10.0 / _numBands));
                
                int startBin = (int)(startFreq * _fftSize / _sampleRate);
                int endBin = (int)(endFreq * _fftSize / _sampleRate);
                
                // Create band filter
                for (int i = 0; i < _bandFilters[band].Length; i++)
                {
                    if (i >= startBin && i <= endBin)
                    {
                        // Smooth transitions at band edges
                        float bandPosition = (float)(i - startBin) / (endBin - startBin);
                        _bandFilters[band][i] = 0.5f * (1f - Mathf.Cos(Mathf.PI * bandPosition));
                    }
                    else
                    {
                        _bandFilters[band][i] = 0f;
                    }
                }
            }
        }
        
        public float[] ProcessAudio(float[] inputAudio)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AINoiseSuppressionProcessor));
            
            if (!IsEnabled || inputAudio == null || inputAudio.Length == 0)
                return inputAudio ?? new float[0];
            
            float[] output = new float[inputAudio.Length];
            
            // Process in overlapping frames
            for (int i = 0; i < inputAudio.Length; i += _hopSize)
            {
                int frameLength = System.Math.Min(_frameSize, inputAudio.Length - i);
                
                // Extract frame with overlap
                float[] frame = ExtractFrame(inputAudio, i, frameLength);
                
                // Process frame
                float[] processedFrame = ProcessFrame(frame);
                
                // Overlap-add to output
                OverlapAdd(output, processedFrame, i);
            }
            
            _frameCounter++;
            return output;
        }
        
        private float[] ExtractFrame(float[] input, int startIndex, int length)
        {
            float[] frame = new float[_frameSize];
            
            // Copy input data
            for (int i = 0; i < length && startIndex + i < input.Length; i++)
            {
                frame[i] = input[startIndex + i];
            }
            
            // Apply window
            for (int i = 0; i < _frameSize; i++)
            {
                frame[i] *= _windowFunction[i];
            }
            
            return frame;
        }
        
        private float[] ProcessFrame(float[] frame)
        {
            // Step 1: Forward FFT
            PrepareFFTBuffer(frame);
            SimpleFFT(_fftBuffer);
            
            // Step 2: Extract magnitude and phase
            ExtractMagnitudeAndPhase();
            
            // Step 3: Voice activity detection for noise learning
            bool voiceActive = _vadForNoise.DetectVoiceActivity(frame);
            
            // Step 4: Update noise model
            UpdateNoiseModel(voiceActive);
            
            // Step 5: Multi-stage noise reduction
            float[] enhancedMagnitude = ApplyMultiStageNoiseReduction(_magnitudeSpectrum);
            
            // Step 6: Reconstruct signal
            ReconstructSignal(enhancedMagnitude, _phaseSpectrum);
            
            // Step 7: Inverse FFT
            SimpleIFFT(_fftBuffer);
            
            // Step 8: Extract and window output
            return ExtractOutputFrame();
        }
        
        private void PrepareFFTBuffer(float[] frame)
        {
            // Zero-pad and prepare for FFT
            for (int i = 0; i < _fftSize; i++)
            {
                if (i < frame.Length)
                {
                    _fftBuffer[i] = new Complex(frame[i], 0);
                }
                else
                {
                    _fftBuffer[i] = Complex.Zero;
                }
            }
        }
        
        private void ExtractMagnitudeAndPhase()
        {
            for (int i = 0; i < _magnitudeSpectrum.Length; i++)
            {
                _magnitudeSpectrum[i] = (float)_fftBuffer[i].Magnitude;
                _phaseSpectrum[i] = (float)_fftBuffer[i].Phase;
            }
        }
        
        private void UpdateNoiseModel(bool voiceActive)
        {
            if (_isLearningNoise && _noiseLearnFrames < _maxNoiseLearnFrames)
            {
                // Learn noise during initial frames or when no voice is detected
                if (!voiceActive || _noiseLearnFrames < 20) // Force learning for first 20 frames
                {
                    for (int i = 0; i < _noiseFloorEstimate.Length; i++)
                    {
                        _noiseFloorEstimate[i] = _noiseFloorEstimate[i] * (1f - _noiseAdaptationRate) +
                                               _magnitudeSpectrum[i] * _noiseAdaptationRate;
                    }
                }
                
                _noiseLearnFrames++;
                if (_noiseLearnFrames >= _maxNoiseLearnFrames)
                {
                    _isLearningNoise = false;
                }
            }
            else if (!voiceActive)
            {
                // Continue adapting noise floor during silence
                for (int i = 0; i < _noiseFloorEstimate.Length; i++)
                {
                    float adaptationRate = _noiseAdaptationRate * 0.1f; // Slower adaptation
                    _noiseFloorEstimate[i] = _noiseFloorEstimate[i] * (1f - adaptationRate) +
                                           _magnitudeSpectrum[i] * adaptationRate;
                }
            }
            
            // Update signal-to-noise ratio
            for (int i = 0; i < _signalToNoiseRatio.Length; i++)
            {
                float currentSNR = _magnitudeSpectrum[i] / (_noiseFloorEstimate[i] + 1e-6f);
                
                // Adapt signal characteristics when voice is active
                if (voiceActive)
                {
                    _signalToNoiseRatio[i] = _signalToNoiseRatio[i] * (1f - _signalAdaptationRate) +
                                           currentSNR * _signalAdaptationRate;
                }
                else
                {
                    _signalToNoiseRatio[i] = currentSNR;
                }
            }
        }
        
        private float[] ApplyMultiStageNoiseReduction(float[] magnitude)
        {
            // Stage 1: Spectral subtraction
            float[] stage1 = _spectralSubtractor.Process(magnitude, _noiseFloorEstimate, NoiseReductionStrength);
            
            // Stage 2: Wiener filtering
            float[] stage2 = _wienerFilter.Process(stage1, _signalToNoiseRatio);
            
            // Stage 3: Neural network enhancement
            float[] stage3 = _neuralReducer.Process(stage2, _noiseFloorEstimate);
            
            // Stage 4: Multi-band processing
            float[] stage4 = ApplyMultiBandProcessing(stage3);
            
            // Stage 5: Residual noise reduction
            float[] stage5 = _residualReducer.Process(stage4, magnitude, _noiseFloorEstimate);
            
            return stage5;
        }
        
        private float[] ApplyMultiBandProcessing(float[] magnitude)
        {
            float[] output = new float[magnitude.Length];
            
            // Update band noise floors
            for (int band = 0; band < _numBands; band++)
            {
                float bandEnergy = 0f;
                float bandNoise = 0f;
                int bandSamples = 0;
                
                for (int i = 0; i < magnitude.Length; i++)
                {
                    if (_bandFilters[band][i] > 0.1f)
                    {
                        bandEnergy += magnitude[i] * _bandFilters[band][i];
                        bandNoise += _noiseFloorEstimate[i] * _bandFilters[band][i];
                        bandSamples++;
                    }
                }
                
                if (bandSamples > 0)
                {
                    bandEnergy /= bandSamples;
                    bandNoise /= bandSamples;
                    
                    // Calculate band-specific gain
                    float bandSNR = bandEnergy / (bandNoise + 1e-6f);
                    _bandGains[band] = CalculateBandGain(bandSNR, band);
                }
            }
            
            // Apply band gains
            for (int i = 0; i < magnitude.Length; i++)
            {
                float totalGain = 0f;
                float totalWeight = 0f;
                
                for (int band = 0; band < _numBands; band++)
                {
                    float weight = _bandFilters[band][i];
                    totalGain += _bandGains[band] * weight;
                    totalWeight += weight;
                }
                
                if (totalWeight > 0)
                {
                    output[i] = magnitude[i] * (totalGain / totalWeight);
                }
                else
                {
                    output[i] = magnitude[i];
                }
            }
            
            return output;
        }
        
        private float CalculateBandGain(float snr, int bandIndex)
        {
            // Frequency-dependent noise reduction
            float baseGain = 1f;
            
            if (snr < 1f) // Low SNR - apply more reduction
            {
                baseGain = Mathf.Lerp(0.1f, 1f, snr);
            }
            else if (snr > 10f) // High SNR - minimal reduction
            {
                baseGain = 1f;
            }
            else // Medium SNR - moderate reduction
            {
                baseGain = Mathf.Lerp(0.5f, 1f, (snr - 1f) / 9f);
            }
            
            // Apply frequency-dependent adjustments
            if (bandIndex < 2) // Low frequencies - preserve more
            {
                baseGain = Mathf.Lerp(baseGain, 1f, 0.3f);
            }
            else if (bandIndex > 5) // High frequencies - reduce more aggressively
            {
                baseGain *= 0.8f;
            }
            
            return Mathf.Clamp(baseGain, 0.05f, 1f);
        }
        
        private void ReconstructSignal(float[] magnitude, float[] phase)
        {
            for (int i = 0; i < magnitude.Length && i < _fftBuffer.Length; i++)
            {
                float real = magnitude[i] * Mathf.Cos(phase[i]);
                float imag = magnitude[i] * Mathf.Sin(phase[i]);
                _fftBuffer[i] = new Complex(real, imag);
            }
            
            // Mirror for real FFT
            for (int i = magnitude.Length; i < _fftSize; i++)
            {
                int mirrorIndex = _fftSize - i;
                if (mirrorIndex < magnitude.Length)
                {
                    _fftBuffer[i] = Complex.Conjugate(_fftBuffer[mirrorIndex]);
                }
                else
                {
                    _fftBuffer[i] = Complex.Zero;
                }
            }
        }
        
        private float[] ExtractOutputFrame()
        {
            float[] output = new float[_frameSize];
            
            for (int i = 0; i < _frameSize; i++)
            {
                output[i] = (float)_fftBuffer[i].Real * _windowFunction[i];
            }
            
            return output;
        }
        
        private void OverlapAdd(float[] output, float[] frame, int startIndex)
        {
            for (int i = 0; i < frame.Length && startIndex + i < output.Length; i++)
            {
                output[startIndex + i] += frame[i];
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
            _isLearningNoise = true;
            _noiseLearnFrames = 0;
            Array.Clear(_noiseFloorEstimate, 0, _noiseFloorEstimate.Length);
            Array.Clear(_overlapBuffer, 0, _overlapBuffer.Length);
            
            for (int i = 0; i < _noiseFloorEstimate.Length; i++)
            {
                _noiseFloorEstimate[i] = 0.001f;
            }
            
            _neuralReducer?.Reset();
            _vadForNoise?.Reset();
            _spectralSubtractor?.Reset();
            _wienerFilter?.Reset();
            _residualReducer?.Reset();
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            
            _neuralReducer?.Dispose();
            _vadForNoise?.Dispose();
            _spectralSubtractor?.Dispose();
            _wienerFilter?.Dispose();
            _residualReducer?.Dispose();
            
            _disposed = true;
        }
    }
    
    // Helper classes for AI noise suppression
    public class NeuralNoiseReducer : IDisposable
    {
        private readonly int _inputSize;
        private readonly float[] _weights;
        private readonly float[] _biases;
        private bool _disposed = false;
        
        public NeuralNoiseReducer(int sampleRate, int inputSize)
        {
            _inputSize = inputSize;
            _weights = new float[inputSize];
            _biases = new float[inputSize];
            
            // Initialize with simple heuristic weights
            InitializeWeights();
        }
        
        private void InitializeWeights()
        {
            // Simple frequency-based weighting
            for (int i = 0; i < _inputSize; i++)
            {
                float freq = (float)i / _inputSize;
                // Emphasize voice frequencies (300-3400 Hz)
                if (freq > 0.1f && freq < 0.7f)
                {
                    _weights[i] = 1.2f;
                }
                else
                {
                    _weights[i] = 0.8f;
                }
                _biases[i] = 0.1f;
            }
        }
        
        public float[] Process(float[] input, float[] noiseFloor)
        {
            if (_disposed) return input;
            
            float[] output = new float[input.Length];
            
            for (int i = 0; i < System.Math.Min(input.Length, _inputSize); i++)
            {
                // Simple neural-like processing
                float snr = input[i] / (noiseFloor[i] + 1e-6f);
                float activation = (float)System.Math.Tanh(snr * _weights[i] + _biases[i]);
                output[i] = input[i] * System.Math.Max(0f, System.Math.Min(1f, activation));
            }
            
            return output;
        }
        
        public void Reset() { }
        
        public void Dispose()
        {
            _disposed = true;
        }
    }
    
    public class SpectralSubtractor : IDisposable
    {
        private readonly int _size;
        private bool _disposed = false;
        
        public SpectralSubtractor(int size)
        {
            _size = size;
        }
        
        public float[] Process(float[] magnitude, float[] noiseFloor, float strength)
        {
            if (_disposed) return magnitude;
            
            float[] output = new float[magnitude.Length];
            
            for (int i = 0; i < magnitude.Length; i++)
            {
                float subtracted = magnitude[i] - noiseFloor[i] * strength;
                output[i] = Mathf.Max(subtracted, magnitude[i] * 0.1f); // Minimum 10% of original
            }
            
            return output;
        }
        
        public void Reset() { }
        
        public void Dispose()
        {
            _disposed = true;
        }
    }
    
    public class WienerFilter : IDisposable
    {
        private readonly int _size;
        private bool _disposed = false;
        
        public WienerFilter(int size)
        {
            _size = size;
        }
        
        public float[] Process(float[] magnitude, float[] snr)
        {
            if (_disposed) return magnitude;
            
            float[] output = new float[magnitude.Length];
            
            for (int i = 0; i < magnitude.Length; i++)
            {
                float gain = snr[i] / (snr[i] + 1f);
                output[i] = magnitude[i] * gain;
            }
            
            return output;
        }
        
        public void Reset() { }
        
        public void Dispose()
        {
            _disposed = true;
        }
    }
    
    public class ResidualNoiseReducer : IDisposable
    {
        private readonly int _size;
        private bool _disposed = false;
        
        public ResidualNoiseReducer(int size)
        {
            _size = size;
        }
        
        public float[] Process(float[] processed, float[] original, float[] noiseFloor)
        {
            if (_disposed) return processed;
            
            float[] output = new float[processed.Length];
            
            for (int i = 0; i < processed.Length; i++)
            {
                // Detect residual noise
                float residualRatio = processed[i] / (original[i] + 1e-6f);
                if (residualRatio < 0.3f && processed[i] < noiseFloor[i] * 2f)
                {
                    // Further reduce suspected residual noise
                    output[i] = processed[i] * 0.5f;
                }
                else
                {
                    output[i] = processed[i];
                }
            }
            
            return output;
        }
        
        public void Reset() { }
        
        public void Dispose()
        {
            _disposed = true;
        }
    }
    
    public class VoiceActivityDetector : IDisposable
    {
        private readonly int _sampleRate;
        private readonly int _frameSize;
        private readonly float[] _energyHistory;
        private readonly int _historySize = 10;
        private int _historyIndex;
        private float _noiseFloor;
        private bool _disposed = false;
        
        public VoiceActivityDetector(int sampleRate, int frameSize)
        {
            _sampleRate = sampleRate;
            _frameSize = frameSize;
            _energyHistory = new float[_historySize];
            _noiseFloor = 0.001f;
        }
        
        public bool DetectVoiceActivity(float[] frame)
        {
            if (_disposed) return false;
            
            float energy = 0f;
            float spectralCentroid = 0f;
            float totalMagnitude = 0f;
            
            // Calculate energy and spectral features
            for (int i = 0; i < frame.Length; i++)
            {
                float magnitude = Mathf.Abs(frame[i]);
                energy += frame[i] * frame[i];
                spectralCentroid += i * magnitude;
                totalMagnitude += magnitude;
            }
            energy /= frame.Length;
            
            // Calculate spectral centroid (frequency center of mass)
            if (totalMagnitude > 0f)
                spectralCentroid /= totalMagnitude;
            
            _energyHistory[_historyIndex] = energy;
            _historyIndex = (_historyIndex + 1) % _historySize;
            
            // Update noise floor more conservatively
            float minEnergy = _energyHistory.Min();
            _noiseFloor = _noiseFloor * 0.995f + minEnergy * 0.005f;
            
            // More aggressive thresholds for voice detection
            bool energyCheck = energy > _noiseFloor * 8f; // Increased from 3f to 8f
            
            // Voice frequency range check (150Hz - 3400Hz mapped to sample indices)
            float voiceFreqStart = 150f * frame.Length / _sampleRate;
            float voiceFreqEnd = 3400f * frame.Length / _sampleRate;
            bool frequencyCheck = spectralCentroid >= voiceFreqStart && spectralCentroid <= voiceFreqEnd;
            
            // Zero crossing rate for voice characteristics
            int zeroCrossings = 0;
            for (int i = 1; i < frame.Length; i++)
            {
                if ((frame[i] >= 0) != (frame[i-1] >= 0))
                    zeroCrossings++;
            }
            float zcr = (float)zeroCrossings / frame.Length;
            bool zcrCheck = zcr > 0.02f && zcr < 0.3f; // Voice typical ZCR range
            
            return energyCheck && frequencyCheck && zcrCheck;
        }
        
        public void Reset()
        {
            Array.Clear(_energyHistory, 0, _energyHistory.Length);
            _historyIndex = 0;
            _noiseFloor = 0.001f;
        }
        
        public void Dispose()
        {
            _disposed = true;
        }
    }
}