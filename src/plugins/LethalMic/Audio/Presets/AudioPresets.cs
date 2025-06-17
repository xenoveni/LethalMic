using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using BepInEx;

namespace LethalMic
{
    [Serializable]
    public class AudioPreset
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime CreatedDate { get; set; }
        
        // Noise Suppression Settings
        public bool NoiseSuppressionEnabled { get; set; } = true;
        public float NoiseSuppressionStrength { get; set; } = 0.8f;
        public bool RNNoiseEnabled { get; set; } = true;
        
        // Voice Enhancement Settings
        public bool VoiceEnhancementEnabled { get; set; } = true;
        public float VoiceGain { get; set; } = 1.0f;
        public bool AutoGainControlEnabled { get; set; } = true;
        
        // Echo Cancellation Settings
        public bool EchoCancellationEnabled { get; set; } = true;
        public float EchoCancellationStrength { get; set; } = 0.7f;
        public int EchoFilterLength { get; set; } = 256;
        
        // Voice Ducking Settings
        public bool VoiceDuckingEnabled { get; set; } = true;
        public float DuckingLevel { get; set; } = 0.3f;
        public float DuckingAttackTime { get; set; } = 0.003f;
        public float DuckingReleaseTime { get; set; } = 0.1f;
        
        // Advanced Settings
        public int ProcessingQuality { get; set; } = 5;
        public bool SpectralSubtractionEnabled { get; set; } = false;
        public bool LoopDetectionEnabled { get; set; } = true;
        public float LoopDetectionThreshold { get; set; } = 0.7f;
        
        public AudioPreset()
        {
            Name = "Default";
            Description = "Default audio processing settings";
            CreatedDate = DateTime.Now;
        }
        
        public AudioPreset(string name, string description) : this()
        {
            Name = name;
            Description = description;
        }
    }
    
    public static class AudioPresetManager
    {
        private static readonly string PresetsDirectory = Path.Combine(Paths.ConfigPath, "LethalMic", "Presets");
        private static readonly Dictionary<string, AudioPreset> _loadedPresets = new Dictionary<string, AudioPreset>();
        private static AudioPreset _currentPreset;
        
        static AudioPresetManager()
        {
            InitializePresets();
        }
        
        public static void Initialize()
        {
            InitializePresets();
        }
        
        private static void InitializePresets()
        {
            try
            {
                // Create presets directory if it doesn't exist
                if (!Directory.Exists(PresetsDirectory))
                {
                    Directory.CreateDirectory(PresetsDirectory);
                }
                
                // Create default presets if they don't exist
                CreateDefaultPresets();
                
                // Load all presets
                LoadAllPresets();
                
                // Set default preset as current
                _currentPreset = GetPreset("Default") ?? CreateDefaultPreset();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to initialize audio presets: {ex.Message}");
                _currentPreset = CreateDefaultPreset();
            }
        }
        
        private static void CreateDefaultPresets()
        {
            // Default preset
            var defaultPreset = CreateDefaultPreset();
            SavePreset(defaultPreset);
            
            // High Quality preset
            var highQualityPreset = new AudioPreset("High Quality", "Maximum quality settings for best audio processing")
            {
                NoiseSuppressionStrength = 0.9f,
                ProcessingQuality = 10,
                EchoFilterLength = 512,
                VoiceGain = 1.2f,
                SpectralSubtractionEnabled = true
            };
            SavePreset(highQualityPreset);
            
            // Performance preset
            var performancePreset = new AudioPreset("Performance", "Optimized for lower CPU usage")
            {
                NoiseSuppressionStrength = 0.6f,
                ProcessingQuality = 3,
                EchoFilterLength = 128,
                SpectralSubtractionEnabled = false,
                RNNoiseEnabled = false
            };
            SavePreset(performancePreset);
            
            // Gaming preset
            var gamingPreset = new AudioPreset("Gaming", "Balanced settings for gaming with voice chat")
            {
                NoiseSuppressionStrength = 0.7f,
                VoiceDuckingEnabled = true,
                DuckingLevel = 0.4f,
                ProcessingQuality = 5,
                LoopDetectionEnabled = true
            };
            SavePreset(gamingPreset);
            
            // Streaming preset
            var streamingPreset = new AudioPreset("Streaming", "Professional settings for content creation")
            {
                NoiseSuppressionStrength = 0.85f,
                VoiceGain = 1.1f,
                ProcessingQuality = 8,
                EchoCancellationStrength = 0.8f,
                SpectralSubtractionEnabled = true,
                VoiceDuckingEnabled = false
            };
            SavePreset(streamingPreset);
        }
        
        private static AudioPreset CreateDefaultPreset()
        {
            return new AudioPreset("Default", "Standard audio processing settings");
        }
        
        public static void SavePreset(AudioPreset preset)
        {
            try
            {
                string filePath = Path.Combine(PresetsDirectory, $"{preset.Name}.json");
                string json = UnityEngine.JsonUtility.ToJson(preset, true);
                File.WriteAllText(filePath, json);
                
                _loadedPresets[preset.Name] = preset;
                UnityEngine.Debug.Log($"Saved audio preset: {preset.Name}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to save preset {preset.Name}: {ex.Message}");
            }
        }
        
        public static AudioPreset LoadPreset(string name)
        {
            try
            {
                string filePath = Path.Combine(PresetsDirectory, $"{name}.json");
                if (!File.Exists(filePath))
                {
                    UnityEngine.Debug.LogWarning($"Preset file not found: {name}");
                    return null;
                }
                
                string json = File.ReadAllText(filePath);
                var preset = UnityEngine.JsonUtility.FromJson<AudioPreset>(json);
                
                _loadedPresets[name] = preset;
                return preset;
            }
            catch (Exception ex)
            {
            UnityEngine.Debug.LogError($"Failed to load preset {name}: {ex.Message}");
                return null;
            }
        }
        
        private static void LoadAllPresets()
        {
            try
            {
                if (!Directory.Exists(PresetsDirectory))
                    return;
                
                string[] presetFiles = Directory.GetFiles(PresetsDirectory, "*.json");
                foreach (string file in presetFiles)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    LoadPreset(name);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to load presets: {ex.Message}");
            }
        }
        
        public static AudioPreset GetPreset(string name)
        {
            _loadedPresets.TryGetValue(name, out AudioPreset preset);
            return preset;
        }
        
        public static List<string> GetPresetNames()
        {
            return new List<string>(_loadedPresets.Keys);
        }
        
        public static AudioPreset GetCurrentPreset()
        {
            return _currentPreset;
        }
        
        public static void SetCurrentPreset(string name)
        {
            var preset = GetPreset(name);
            if (preset != null)
            {
                _currentPreset = preset;
                UnityEngine.Debug.Log($"Switched to audio preset: {name}");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"Preset not found: {name}");
            }
        }
        
        public static void SetCurrentPreset(AudioPreset preset)
        {
            if (preset != null)
            {
                _currentPreset = preset;
                UnityEngine.Debug.Log($"Applied audio preset: {preset.Name}");
            }
        }
        
        public static void DeletePreset(string name)
        {
            try
            {
                if (name == "Default")
                {
                    UnityEngine.Debug.LogWarning("Cannot delete the default preset");
                    return;
                }
                
                string filePath = Path.Combine(PresetsDirectory, $"{name}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                
                _loadedPresets.Remove(name);
                
                // If current preset was deleted, switch to default
                if (_currentPreset?.Name == name)
                {
                    SetCurrentPreset("Default");
                }
                
                UnityEngine.Debug.Log($"Deleted audio preset: {name}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to delete preset {name}: {ex.Message}");
            }
        }
        
        public static AudioPreset CreatePresetFromCurrent(string name, string description)
        {
            var newPreset = new AudioPreset(name, description);
            
            // Copy current settings (this would be populated from actual plugin settings)
            if (_currentPreset != null)
            {
                newPreset.NoiseSuppressionEnabled = _currentPreset.NoiseSuppressionEnabled;
                newPreset.NoiseSuppressionStrength = _currentPreset.NoiseSuppressionStrength;
                newPreset.RNNoiseEnabled = _currentPreset.RNNoiseEnabled;
                newPreset.VoiceEnhancementEnabled = _currentPreset.VoiceEnhancementEnabled;
                newPreset.VoiceGain = _currentPreset.VoiceGain;
                newPreset.AutoGainControlEnabled = _currentPreset.AutoGainControlEnabled;
                newPreset.EchoCancellationEnabled = _currentPreset.EchoCancellationEnabled;
                newPreset.EchoCancellationStrength = _currentPreset.EchoCancellationStrength;
                newPreset.EchoFilterLength = _currentPreset.EchoFilterLength;
                newPreset.VoiceDuckingEnabled = _currentPreset.VoiceDuckingEnabled;
                newPreset.DuckingLevel = _currentPreset.DuckingLevel;
                newPreset.DuckingAttackTime = _currentPreset.DuckingAttackTime;
                newPreset.DuckingReleaseTime = _currentPreset.DuckingReleaseTime;
                newPreset.ProcessingQuality = _currentPreset.ProcessingQuality;
                newPreset.SpectralSubtractionEnabled = _currentPreset.SpectralSubtractionEnabled;
                newPreset.LoopDetectionEnabled = _currentPreset.LoopDetectionEnabled;
                newPreset.LoopDetectionThreshold = _currentPreset.LoopDetectionThreshold;
            }
            
            SavePreset(newPreset);
            return newPreset;
        }
    }
}