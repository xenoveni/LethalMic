using System;
using UnityEngine;

namespace LethalMic
{
    public class AdaptiveNoiseFloorEstimator : IDisposable
    {
        private readonly int _sampleRate;
        private readonly float _adaptationRate;
        private readonly float _minNoiseFloor;
        private readonly float _maxNoiseFloor;
        private readonly int _windowSize;
        private readonly float[] _energyHistory;
        private int _historyIndex;
        private float _currentNoiseFloor;
        private float _longTermAverage;
        private float _shortTermAverage;
        private bool _disposed = false;
        private int _frameCount = 0;

        public AdaptiveNoiseFloorEstimator(int sampleRate, float adaptationRate = 0.01f)
        {
            _sampleRate = sampleRate;
            _adaptationRate = adaptationRate;
            _minNoiseFloor = -60f; // dB
            _maxNoiseFloor = -20f; // dB
            _windowSize = System.Math.Max(1, sampleRate / 100); // 10ms window
            _energyHistory = new float[100]; // Store 100 energy measurements
            _historyIndex = 0;
            _currentNoiseFloor = -40f; // Initial estimate in dB
            _longTermAverage = 0f;
            _shortTermAverage = 0f;
        }

        public float EstimateNoiseFloor(float[] data, int offset, int length)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AdaptiveNoiseFloorEstimator));
            
            // Calculate RMS energy of the current frame
            float energy = CalculateRMSEnergy(data, offset, length);
            float energyDb = 20f * Mathf.Log10(Mathf.Max(energy, 1e-10f));
            
            // Store in circular buffer
            _energyHistory[_historyIndex] = energyDb;
            _historyIndex = (_historyIndex + 1) % _energyHistory.Length;
            _frameCount++;
            
            // Update short-term and long-term averages
            UpdateAverages(energyDb);
            
            // Adaptive noise floor estimation
            if (_frameCount > _energyHistory.Length)
            {
                // Find minimum energy in recent history (likely noise)
                float minRecentEnergy = float.MaxValue;
                float avgRecentEnergy = 0f;
                int validSamples = System.Math.Min(_frameCount, _energyHistory.Length);
                
                for (int i = 0; i < validSamples; i++)
                {
                    minRecentEnergy = System.Math.Min(minRecentEnergy, _energyHistory[i]);
                    avgRecentEnergy += _energyHistory[i];
                }
                avgRecentEnergy /= validSamples;
                
                // Adaptive threshold based on energy distribution
                float energyVariance = CalculateEnergyVariance(avgRecentEnergy, validSamples);
                float adaptiveThreshold = minRecentEnergy + energyVariance * 0.5f;
                
                // Smooth adaptation
                float targetNoiseFloor = Mathf.Clamp(adaptiveThreshold, _minNoiseFloor, _maxNoiseFloor);
                _currentNoiseFloor = Mathf.Lerp(_currentNoiseFloor, targetNoiseFloor, _adaptationRate);
                
                // Voice activity detection influence
                bool likelyVoice = DetectVoiceActivity(energyDb, energyVariance);
                if (likelyVoice)
                {
                    // Slower adaptation during voice activity
                    _currentNoiseFloor = Mathf.Lerp(_currentNoiseFloor, targetNoiseFloor, _adaptationRate * 0.1f);
                }
            }
            
            return _currentNoiseFloor;
        }
        
        private float CalculateRMSEnergy(float[] data, int offset, int length)
        {
            float sum = 0f;
            int count = 0;
            
            for (int i = offset; i < offset + length && i < data.Length; i++)
            {
                sum += data[i] * data[i];
                count++;
            }
            
            return count > 0 ? Mathf.Sqrt(sum / count) : 0f;
        }
        
        private void UpdateAverages(float energyDb)
        {
            // Short-term average (last 10 frames)
            _shortTermAverage = _shortTermAverage * 0.9f + energyDb * 0.1f;
            
            // Long-term average (last 100 frames)
            _longTermAverage = _longTermAverage * 0.99f + energyDb * 0.01f;
        }
        
        private float CalculateEnergyVariance(float avgEnergy, int validSamples)
        {
            float variance = 0f;
            
            for (int i = 0; i < validSamples; i++)
            {
                float diff = _energyHistory[i] - avgEnergy;
                variance += diff * diff;
            }
            
            return validSamples > 1 ? Mathf.Sqrt(variance / (validSamples - 1)) : 0f;
        }
        
        private bool DetectVoiceActivity(float currentEnergyDb, float energyVariance)
        {
            // Simple voice activity detection based on energy characteristics
            float energyThreshold = _currentNoiseFloor + 6f; // 6dB above noise floor
            bool energyAboveThreshold = currentEnergyDb > energyThreshold;
            
            // Voice typically has higher variance than steady noise
            bool hasVoiceVariance = energyVariance > 3f;
            
            // Short-term energy significantly above long-term average
            bool energyIncrease = _shortTermAverage > _longTermAverage + 3f;
            
            return energyAboveThreshold && (hasVoiceVariance || energyIncrease);
        }
        
        public float GetCurrentNoiseFloor()
        {
            return _currentNoiseFloor;
        }
        
        public void Reset()
        {
            _historyIndex = 0;
            _frameCount = 0;
            _currentNoiseFloor = -40f;
            _longTermAverage = 0f;
            _shortTermAverage = 0f;
            System.Array.Clear(_energyHistory, 0, _energyHistory.Length);
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}