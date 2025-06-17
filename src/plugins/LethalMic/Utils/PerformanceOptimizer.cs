using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Threading;

namespace LethalMic
{
    /// <summary>
    /// Handles performance optimization and external library management
    /// </summary>
    public class PerformanceOptimizer : IDisposable
    {
        private readonly int _sampleRate;
        private readonly int _channels;
        private bool _disposed = false;
        
        // Performance monitoring
        private readonly Stopwatch _performanceTimer = new Stopwatch();
        private readonly Queue<float> _cpuUsageHistory = new Queue<float>();
        private const int MAX_HISTORY_SIZE = 100;
        
        // External library management
        private bool _rnnoiseDllAvailable = false;
        private bool _opusLibraryOptimized = false;
        
        // Codec optimization
        private readonly Dictionary<int, byte[]> _opusBufferPool = new Dictionary<int, byte[]>();
        private readonly object _bufferPoolLock = new object();
        
        public PerformanceOptimizer(int sampleRate, int channels)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            
            InitializeExternalLibraries();
            OptimizeCodecSettings();
        }
        
        private void InitializeExternalLibraries()
        {
            // Check RNNoise availability
            try
            {
                // Try to load RNNoise library
                IntPtr testPtr = rnnoise_create(IntPtr.Zero);
                if (testPtr != IntPtr.Zero)
                {
                    rnnoise_destroy(testPtr);
                    _rnnoiseDllAvailable = true;
                    UnityEngine.Debug.Log("RNNoise library successfully loaded");
                }
            }
            catch (DllNotFoundException)
            {
                UnityEngine.Debug.LogWarning("RNNoise library not found. Noise suppression will use fallback algorithms.");
                _rnnoiseDllAvailable = false;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"RNNoise initialization failed: {ex.Message}");
                _rnnoiseDllAvailable = false;
            }
        }
        
        private void OptimizeCodecSettings()
        {
            try
            {
                // Pre-allocate Opus buffers for different frame sizes
                int[] commonFrameSizes = { 120, 240, 480, 960, 1920 };
                
                lock (_bufferPoolLock)
                {
                    foreach (int frameSize in commonFrameSizes)
                    {
                        int bufferSize = frameSize * _channels * 4; // 4 bytes per sample for safety
                        _opusBufferPool[frameSize] = new byte[bufferSize];
                    }
                }
                
                _opusLibraryOptimized = true;
                UnityEngine.Debug.Log("Opus codec buffers optimized");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Opus optimization failed: {ex.Message}");
                _opusLibraryOptimized = false;
            }
        }
        
        public void StartPerformanceMonitoring()
        {
            _performanceTimer.Start();
        }
        
        public float StopPerformanceMonitoring()
        {
            _performanceTimer.Stop();
            float cpuUsage = (float)_performanceTimer.Elapsed.TotalMilliseconds;
            
            // Add to history
            _cpuUsageHistory.Enqueue(cpuUsage);
            if (_cpuUsageHistory.Count > MAX_HISTORY_SIZE)
            {
                _cpuUsageHistory.Dequeue();
            }
            
            _performanceTimer.Reset();
            return cpuUsage;
        }
        
        public float GetAverageCpuUsage()
        {
            if (_cpuUsageHistory.Count == 0) return 0f;
            
            float total = 0f;
            foreach (float usage in _cpuUsageHistory)
            {
                total += usage;
            }
            
            return total / _cpuUsageHistory.Count;
        }
        
        public byte[] GetOptimizedOpusBuffer(int frameSize)
        {
            lock (_bufferPoolLock)
            {
                if (_opusBufferPool.TryGetValue(frameSize, out byte[] buffer))
                {
                    return buffer;
                }
                
                // Create new buffer if not found
                int bufferSize = frameSize * _channels * 4;
                byte[] newBuffer = new byte[bufferSize];
                _opusBufferPool[frameSize] = newBuffer;
                return newBuffer;
            }
        }
        
        public bool IsRNNoiseAvailable => _rnnoiseDllAvailable;
        public bool IsOpusOptimized => _opusLibraryOptimized;
        
        public PerformanceRecommendation GetPerformanceRecommendation()
        {
            float avgCpu = GetAverageCpuUsage();
            
            if (avgCpu > 10f)
            {
                return new PerformanceRecommendation
                {
                    RecommendedFFTSize = 512,
                    RecommendedQuality = 1,
                    DisableAIProcessing = true,
                    Message = "High CPU usage detected. Consider reducing processing quality."
                };
            }
            else if (avgCpu > 5f)
            {
                return new PerformanceRecommendation
                {
                    RecommendedFFTSize = 1024,
                    RecommendedQuality = 2,
                    DisableAIProcessing = false,
                    Message = "Moderate CPU usage. Current settings are acceptable."
                };
            }
            else
            {
                return new PerformanceRecommendation
                {
                    RecommendedFFTSize = 2048,
                    RecommendedQuality = 3,
                    DisableAIProcessing = false,
                    Message = "Low CPU usage. You can increase quality settings."
                };
            }
        }
        
        public void OptimizeForLowEndSystem()
        {
            UnityEngine.Debug.Log("Applying low-end system optimizations");
            
            // Clear buffer pools to save memory
            lock (_bufferPoolLock)
            {
                _opusBufferPool.Clear();
                
                // Only keep essential buffer sizes
                _opusBufferPool[480] = new byte[480 * _channels * 2];
            }
            
            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        
        // RNNoise P/Invoke declarations
        [DllImport("rnnoise", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr rnnoise_create(IntPtr model);
        
        [DllImport("rnnoise", CallingConvention = CallingConvention.Cdecl)]
        private static extern void rnnoise_destroy(IntPtr st);
        
        public void Dispose()
        {
            if (_disposed) return;
            
            _performanceTimer?.Stop();
            _cpuUsageHistory.Clear();
            
            lock (_bufferPoolLock)
            {
                _opusBufferPool.Clear();
            }
            
            _disposed = true;
        }
    }
    
    public class PerformanceRecommendation
    {
        public int RecommendedFFTSize { get; set; }
        public int RecommendedQuality { get; set; }
        public bool DisableAIProcessing { get; set; }
        public string Message { get; set; }
    }
}