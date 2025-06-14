using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace LethalMic
{
    public class VoiceDuckingProcessor : IDisposable
    {
        private readonly int _sampleRate;
        private readonly int _frameSize;
        private readonly float _attackTime;
        private readonly float _releaseTime;
        private readonly float _threshold;
        private readonly float _ratio;
        private readonly float _makeupGain;
        
        // Envelope follower for smooth gain reduction
        private float _envelope;
        private readonly float _attackCoeff;
        private readonly float _releaseCoeff;
        
        // Voice activity detection
        private readonly float[] _energyHistory;
        private readonly int _energyHistorySize = 10;
        private int _energyHistoryIndex;
        private float _noiseFloor;
        private readonly float _noiseFloorAdaptationRate = 0.001f;
        
        // Spectral analysis for voice detection
        private readonly float[] _spectralCentroidHistory;
        private readonly int _spectralHistorySize = 5;
        private int _spectralHistoryIndex;
        
        // Ducking state
        private bool _isDucking;
        private float _duckingGain;
        private readonly float _duckingLevel;
        private readonly float _duckingSmoothingTime;
        private readonly float _duckingSmoothingCoeff;
        
        // Frequency analysis
        private readonly float _voiceFreqMin = 300f;   // Hz
        private readonly float _voiceFreqMax = 3400f;  // Hz
        private readonly float[] _frequencyBins;
        private readonly int _fftSize = 512;
        
        private bool _disposed = false;
        
        public VoiceDuckingProcessor(int sampleRate, int frameSize, float duckingLevel = 0.3f, 
                                   float threshold = -30f, float ratio = 4f, 
                                   float attackTime = 0.003f, float releaseTime = 0.1f)
        {
            _sampleRate = sampleRate;
            _frameSize = frameSize;
            _threshold = DecibelToLinear(threshold);
            _ratio = ratio;
            _attackTime = attackTime;
            _releaseTime = releaseTime;
            _makeupGain = 1f;
            _duckingLevel = Mathf.Clamp01(duckingLevel);
            _duckingSmoothingTime = 0.05f; // 50ms smoothing
            
            // Calculate envelope coefficients
            _attackCoeff = Mathf.Exp(-1f / (_attackTime * _sampleRate));
            _releaseCoeff = Mathf.Exp(-1f / (_releaseTime * _sampleRate));
            _duckingSmoothingCoeff = Mathf.Exp(-1f / (_duckingSmoothingTime * _sampleRate));
            
            // Initialize history buffers
            _energyHistory = new float[_energyHistorySize];
            _spectralCentroidHistory = new float[_spectralHistorySize];
            _noiseFloor = 0.001f; // Initial noise floor estimate
            _envelope = 0f;
            _duckingGain = 1f;
            _isDucking = false;
            
            // Initialize frequency analysis
            _frequencyBins = new float[_fftSize / 2 + 1];
        }
        
        public void ProcessGameAudio(bool voiceDetected)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VoiceDuckingProcessor));
            
            // Update ducking state based on voice detection
            UpdateDuckingState(voiceDetected);
        }
        
        public float[] ProcessAudio(float[] inputAudio, float[] gameAudio, bool voiceDetected)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VoiceDuckingProcessor));
            
            if (inputAudio == null || gameAudio == null)
                return gameAudio ?? new float[0];
            
            int length = System.Math.Min(inputAudio.Length, gameAudio.Length);
            float[] output = new float[length];
            
            // Analyze voice activity in input audio
            bool currentVoiceActivity = AnalyzeVoiceActivity(inputAudio);
            
            // Update ducking state
            UpdateDuckingState(currentVoiceActivity || voiceDetected);
            
            // Process audio with ducking
            for (int i = 0; i < length; i++)
            {
                // Apply ducking to game audio
                float duckingMultiplier = _isDucking ? _duckingLevel : 1f;
                
                // Update envelope follower for smooth gain changes
                float targetGain = duckingMultiplier;
                float coeff = targetGain < _envelope ? _attackCoeff : _releaseCoeff;
                _envelope = _envelope * coeff + targetGain * (1f - coeff);
                
                // Apply envelope and makeup gain
                output[i] = gameAudio[i] * _envelope * _makeupGain;
            }
            
            return output;
        }
        
        private bool AnalyzeVoiceActivity(float[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                return false;
            
            // Calculate RMS energy
            float energy = CalculateRMSEnergy(audioData);
            
            // Update noise floor estimation
            UpdateNoiseFloor(energy);
            
            // Store energy in history
            _energyHistory[_energyHistoryIndex] = energy;
            _energyHistoryIndex = (_energyHistoryIndex + 1) % _energyHistorySize;
            
            // Calculate spectral centroid for voice characteristics
            float spectralCentroid = CalculateSpectralCentroid(audioData);
            _spectralCentroidHistory[_spectralHistoryIndex] = spectralCentroid;
            _spectralHistoryIndex = (_spectralHistoryIndex + 1) % _spectralHistorySize;
            
            // Voice activity detection based on multiple criteria
            bool energyAboveThreshold = energy > _noiseFloor * 3f; // 3x noise floor
            bool spectralCharacteristics = IsVoiceLikeSpectrum(spectralCentroid);
            bool sustainedActivity = IsSustainedActivity();
            
            return energyAboveThreshold && spectralCharacteristics && sustainedActivity;
        }
        
        private float CalculateRMSEnergy(float[] audioData)
        {
            float sum = 0f;
            for (int i = 0; i < audioData.Length; i++)
            {
                sum += audioData[i] * audioData[i];
            }
            return Mathf.Sqrt(sum / audioData.Length);
        }
        
        private void UpdateNoiseFloor(float currentEnergy)
        {
            // Adaptive noise floor estimation
            // Only update if energy is relatively low (likely noise)
            if (currentEnergy < _noiseFloor * 2f || _noiseFloor == 0f)
            {
                _noiseFloor = _noiseFloor * (1f - _noiseFloorAdaptationRate) + 
                             currentEnergy * _noiseFloorAdaptationRate;
            }
            
            // Ensure minimum noise floor
            _noiseFloor = Mathf.Max(_noiseFloor, 0.0001f);
        }
        
        private float CalculateSpectralCentroid(float[] audioData)
        {
            if (audioData.Length < _fftSize)
                return 0f;
            
            // Simple spectral centroid calculation
            // In a real implementation, you'd use FFT here
            float weightedSum = 0f;
            float magnitudeSum = 0f;
            
            // Simplified approach: analyze frequency content in voice range
            int voiceStartSample = (int)(_voiceFreqMin * audioData.Length / _sampleRate);
            int voiceEndSample = (int)(_voiceFreqMax * audioData.Length / _sampleRate);
            
            for (int i = voiceStartSample; i < System.Math.Min(voiceEndSample, audioData.Length); i++)
            {
                float magnitude = Mathf.Abs(audioData[i]);
                float frequency = (float)i * _sampleRate / audioData.Length;
                
                weightedSum += frequency * magnitude;
                magnitudeSum += magnitude;
            }
            
            return magnitudeSum > 0 ? weightedSum / magnitudeSum : 0f;
        }
        
        private bool IsVoiceLikeSpectrum(float spectralCentroid)
        {
            // Check if spectral centroid is in typical voice range
            return spectralCentroid >= _voiceFreqMin && spectralCentroid <= _voiceFreqMax;
        }
        
        private bool IsSustainedActivity()
        {
            // Check for sustained energy over recent history
            int activeFrames = 0;
            float avgEnergy = _energyHistory.Average();
            
            for (int i = 0; i < _energyHistorySize; i++)
            {
                if (_energyHistory[i] > _noiseFloor * 2f)
                {
                    activeFrames++;
                }
            }
            
            // Require at least 30% of recent frames to be active
            return activeFrames >= _energyHistorySize * 0.3f;
        }
        
        private void UpdateDuckingState(bool voiceActive)
        {
            if (voiceActive && !_isDucking)
            {
                // Start ducking
                _isDucking = true;
            }
            else if (!voiceActive && _isDucking)
            {
                // Check if we should stop ducking
                // Add some hysteresis to prevent rapid on/off switching
                if (!HasRecentVoiceActivity())
                {
                    _isDucking = false;
                }
            }
        }
        
        private bool HasRecentVoiceActivity()
        {
            // Check if there was voice activity in recent history
            int recentActiveFrames = 0;
            int checkFrames = System.Math.Min(3, _energyHistorySize); // Check last 3 frames
            
            for (int i = 0; i < checkFrames; i++)
            {
                int index = (_energyHistoryIndex - 1 - i + _energyHistorySize) % _energyHistorySize;
                if (_energyHistory[index] > _noiseFloor * 3f)
                {
                    recentActiveFrames++;
                }
            }
            
            return recentActiveFrames > 0;
        }
        
        public float ProcessSample(float inputSample, float gameAudioSample, bool voiceDetected)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VoiceDuckingProcessor));
            
            // Simple per-sample processing for real-time applications
            float duckingMultiplier = (_isDucking || voiceDetected) ? _duckingLevel : 1f;
            
            // Smooth the ducking transition
            _duckingGain = _duckingGain * _duckingSmoothingCoeff + 
                          duckingMultiplier * (1f - _duckingSmoothingCoeff);
            
            return gameAudioSample * _duckingGain;
        }
        
        public void SetDuckingLevel(float level)
        {
            // Allow runtime adjustment of ducking level
            float newLevel = Mathf.Clamp01(level);
            // Smooth transition to new level
            // This would be implemented with gradual adjustment in a real scenario
        }
        
        public bool IsDucking => _isDucking;
        public float CurrentDuckingGain => _duckingGain;
        public float NoiseFloor => _noiseFloor;
        
        private static float DecibelToLinear(float decibel)
        {
            return Mathf.Pow(10f, decibel / 20f);
        }
        
        private static float LinearToDecibel(float linear)
        {
            return 20f * Mathf.Log10(Mathf.Max(linear, 0.0001f));
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}