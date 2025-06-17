using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using BepInEx.Logging;

namespace LethalMic.Audio.Processing
{
    public class WebRTCAudioProcessor
    {
        private static ManualLogSource Logger;
        private static float[] _processedSamples;
        private static float _lastLoggedAvg;
        private static DateTime _lastSummaryLog;
        private static int _errorCount;
        private static int _audioFrameCount;
        private static float _cpuUsage;
        private static float _peakMicLevel;
        private static float _currentMicrophoneLevel;
        private static bool _voiceDetected;
        private static float _noiseFloor;
        private static bool _isRecording;
        private static string _selectedDevice;
        private static AudioClip _microphoneClip;

        public static void Initialize(ManualLogSource logger)
        {
            Logger = logger;
            _processedSamples = new float[256];
            _lastLoggedAvg = -1f;
            _lastSummaryLog = DateTime.MinValue;
            _errorCount = 0;
            _audioFrameCount = 0;
            _cpuUsage = 0f;
            _peakMicLevel = -60f;
            _currentMicrophoneLevel = -60f;
            _voiceDetected = false;
            _noiseFloor = -60f;
            _isRecording = false;
            _selectedDevice = null;
            _microphoneClip = null;
        }

        public static void ProcessAudio(float[] samples, int sampleCount)
        {
            if (!_isRecording || _microphoneClip == null) return;

            try
            {
                // Process audio samples
                float sum = 0f;
                for (int i = 0; i < sampleCount; i++)
                {
                    sum += samples[i] * samples[i];
                }
                float rms = Mathf.Sqrt(sum / sampleCount);
                _currentMicrophoneLevel = rms;
                _peakMicLevel = Mathf.Max(_peakMicLevel * 0.95f, _currentMicrophoneLevel);
                _voiceDetected = _currentMicrophoneLevel > _noiseFloor;
                _cpuUsage = Mathf.Lerp(_cpuUsage, _currentMicrophoneLevel * 100f, Time.deltaTime);

                // Log significant changes
                if (Math.Abs(_currentMicrophoneLevel - _lastLoggedAvg) > 0.01f)
                {
                    float dbLevel = 20 * Mathf.Log10(Mathf.Max(_currentMicrophoneLevel, 0.0001f));
                    Logger.LogInfo($"[AUDIO] Level changed: {_currentMicrophoneLevel:F4} ({dbLevel:F1} dB), Voice: {_voiceDetected}");
                    _lastLoggedAvg = _currentMicrophoneLevel;
                }

                _audioFrameCount++;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error processing audio: {ex}");
                _errorCount++;
                _currentMicrophoneLevel = 0f;
            }
        }

        public static void StartRecording(string deviceName)
        {
            if (_isRecording) return;

            try
            {
                _selectedDevice = deviceName;
                _microphoneClip = Microphone.Start(_selectedDevice, true, 10, 44100);
                _isRecording = true;
                _audioFrameCount = 0;
                Logger.LogInfo($"Recording started on device: {_selectedDevice}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to start recording: {ex}");
                _errorCount++;
            }
        }

        public static void StopRecording()
        {
            if (!_isRecording) return;

            try
            {
                Microphone.End(_selectedDevice);
                _isRecording = false;

                if (_microphoneClip != null)
                {
                    UnityEngine.Object.Destroy(_microphoneClip);
                    _microphoneClip = null;
                }

                Logger.LogInfo("Recording stopped");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to stop recording: {ex}");
                _errorCount++;
            }
        }

        public static float GetCurrentMicrophoneLevel() => _currentMicrophoneLevel;
        public static bool IsVoiceDetected() => _voiceDetected;
        public static float GetNoiseFloor() => _noiseFloor;
        public static float GetCPUUsage() => _cpuUsage;
        public static int GetErrorCount() => _errorCount;
        public static int GetAudioFrameCount() => _audioFrameCount;
    }
}