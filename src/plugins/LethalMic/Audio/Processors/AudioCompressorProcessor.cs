using System;
using UnityEngine;

namespace LethalMic
{
    public class AudioCompressorProcessor : IDisposable
    {
        private readonly int _sampleRate;
        private float _attackTime;
        private float _releaseTime;
        private float _threshold;
        private float _ratio;
        private float _makeupGain;
        
        // Envelope follower for smooth gain reduction
        private float _envelope;
        private float _attackCoeff;
        private float _releaseCoeff;
        
        // Compression state
        private bool _isEnabled;
        // Remove unused field to suppress warning
        // private float _compressionGain;
        
        private bool _disposed = false;
        
        public bool IsEnabled 
        { 
            get => _isEnabled; 
            set => _isEnabled = value; 
        }
        
        public float Threshold { get; set; } = -20f; // dB
        public float Ratio { get; set; } = 4f;
        public float AttackTime { get; set; } = 10f; // ms
        public float ReleaseTime { get; set; } = 100f; // ms
        public float MakeupGain { get; set; } = 0f; // dB
        
        public AudioCompressorProcessor(int sampleRate, float threshold = -20f, float ratio = 4f, 
                                      float attackTime = 10f, float releaseTime = 100f, float makeupGain = 0f)
        {
            _sampleRate = sampleRate;
            _threshold = DecibelToLinear(threshold);
            _ratio = ratio;
            _attackTime = attackTime;
            _releaseTime = releaseTime;
            _makeupGain = DecibelToLinear(makeupGain);
            
            // Calculate envelope coefficients
            CalculateCoefficients();
            
            _envelope = 0f;
            _isEnabled = true;
        }
        
        private void CalculateCoefficients()
        {
            _attackCoeff = Mathf.Exp(-1f / (_attackTime * 0.001f * _sampleRate));
            _releaseCoeff = Mathf.Exp(-1f / (_releaseTime * 0.001f * _sampleRate));
        }
        
        public float[] ProcessAudio(float[] inputAudio)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AudioCompressorProcessor));
            
            if (!_isEnabled || inputAudio == null || inputAudio.Length == 0)
                return inputAudio ?? new float[0];
            
            float[] output = new float[inputAudio.Length];
            
            for (int i = 0; i < inputAudio.Length; i++)
            {
                // Calculate input level
                float inputLevel = Mathf.Abs(inputAudio[i]);
                
                // Update envelope follower
                float targetEnvelope = inputLevel;
                float coeff = targetEnvelope > _envelope ? _attackCoeff : _releaseCoeff;
                _envelope = _envelope * coeff + targetEnvelope * (1f - coeff);
                
                // Calculate compression gain
                float compressionGain = CalculateCompressionGain(_envelope);
                
                // Apply compression and makeup gain
                output[i] = inputAudio[i] * compressionGain * _makeupGain;
            }
            
            return output;
        }
        
        private float CalculateCompressionGain(float inputLevel)
        {
            if (inputLevel <= _threshold)
                return 1f; // No compression below threshold
            
            // Calculate compression amount
            float overThreshold = inputLevel - _threshold;
            float compressionAmount = overThreshold * (1f - 1f / _ratio);
            float compressedLevel = inputLevel - compressionAmount;
            
            // Calculate gain reduction
            float gainReduction = compressedLevel / inputLevel;
            
            return Mathf.Clamp(gainReduction, 0.001f, 1f); // Prevent division by zero
        }
        
        public void UpdateSettings(float threshold, float ratio, float attackTime, float releaseTime, float makeupGain)
        {
            Threshold = threshold;
            Ratio = ratio;
            AttackTime = attackTime;
            ReleaseTime = releaseTime;
            MakeupGain = makeupGain;
            
            // Update internal fields
            _threshold = DecibelToLinear(threshold);
            _ratio = ratio;
            _attackTime = attackTime;
            _releaseTime = releaseTime;
            _makeupGain = DecibelToLinear(makeupGain);
            
            // Recalculate coefficients
            CalculateCoefficients();
        }
        
        public void Reset()
        {
            _envelope = 0f;
        }
        
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