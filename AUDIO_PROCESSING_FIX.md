# LethalMic Audio Processing Fix

## Problem Identified

The original LethalMic mod had a critical issue: **UI settings were being saved to configuration but NOT applied to the actual audio processing pipeline**. The mod was only applying basic gain and noise gate threshold, while ignoring all the advanced audio processors.

## Root Cause

1. **UI Settings Worked**: Settings were properly saved to BepInEx configuration
2. **Configuration Loading Worked**: Settings were loaded on startup
3. **Audio Processing Was Basic**: Only applied gain and simple noise gate
4. **Advanced Processors Unused**: Sophisticated processors existed but were never called

## Solution Implemented

### 1. Created Integrated Audio Processing Pipeline

- **StaticAudioManager**: Complete rewrite with proper audio processing chain
- **AudioCompressorProcessor**: New processor for compression settings
- **Real-time Processing**: Audio samples are now processed through multiple stages

### 2. Processing Pipeline

```
Raw Microphone Input
    ↓
Apply Microphone Gain (UI Setting)
    ↓
Apply Noise Gate (UI Setting)
    ↓
AI Noise Suppression
    ↓
Spectral Subtraction
    ↓
Audio Compression (UI Settings: Ratio, Attack, Release)
    ↓
Processed Audio Output
```

### 3. Settings Integration

All UI settings now properly affect the audio processing:

- **Microphone Gain**: Applied to raw input
- **Noise Gate**: Threshold-based noise reduction
- **Compression**: Dynamic range compression with configurable ratio, attack, and release times
- **Noise Suppression**: AI-powered noise reduction
- **Spectral Processing**: Frequency-domain noise reduction

### 4. Real-time Updates

Settings changes in the UI immediately update the processing pipeline:

```csharp
public static void SetMicrophoneGain(float value) 
{ 
    if (MicrophoneGain != null) 
    {
        MicrophoneGain.Value = value;
        // Update processor settings when gain changes
        StaticAudioManager.UpdateProcessorSettings();
    }
}
```

## Files Modified

### Core Changes
- `src/plugins/LethalMic/Audio/Management/StaticAudioManager.cs` - Complete rewrite
- `src/plugins/LethalMic/Core/LethalMicStatic.cs` - Integration with new audio manager
- `src/plugins/LethalMic/UI/Components/LethalMicUI.cs` - Updated setting handlers

### New Files
- `src/plugins/LethalMic/Audio/Processors/AudioCompressorProcessor.cs` - Compression processor

## Testing the Fix

### 1. Build and Install
```bash
# Build the mod
dotnet build

# Install to Lethal Company mods folder
# (Copy the built DLL to your BepInEx/plugins folder)
```

### 2. Test Settings Application

1. **Start Lethal Company**
2. **Open LethalMic UI** (Press M key)
3. **Adjust Settings**:
   - Change microphone gain (should affect input level)
   - Toggle noise gate (should reduce background noise)
   - Enable compression and adjust ratio (should compress dynamic range)
   - Adjust attack/release times (should affect compression response)

### 3. Verify Processing

Check the BepInEx log file (`BepInEx/LogOutput.log`) for these messages:

```
[INFO] Updating audio processor settings...
[INFO] Noise suppressor: Enabled=True, Strength=0.01
[INFO] Compressor: Enabled=True, Ratio=4, Attack=10ms, Release=100ms
[INFO] Audio processor settings updated successfully
```

### 4. Audio Quality Tests

- **Noise Gate**: Speak softly, then loudly - background noise should be reduced
- **Compression**: Speak with varying volumes - loud parts should be compressed
- **Gain**: Adjust gain slider - overall volume should change
- **Attack/Release**: Quick sounds should be affected by attack time, sustained sounds by release time

## Expected Results

After this fix:

1. **UI Settings Work**: All sliders and toggles affect audio processing
2. **Real-time Processing**: Changes apply immediately without restart
3. **Advanced Features**: AI noise suppression, spectral processing, and compression work
4. **Better Audio Quality**: Reduced noise, controlled dynamics, cleaner voice
5. **No Audio Looping**: Proper processing prevents feedback loops

## Troubleshooting

### Settings Not Applying
- Check BepInEx logs for error messages
- Verify StaticAudioManager is initialized
- Ensure processors are created successfully

### Audio Issues
- Check microphone device selection
- Verify sample rate compatibility (44.1kHz)
- Monitor CPU usage (processing is intensive)

### Performance Issues
- Reduce buffer size if needed
- Disable unused processors
- Monitor frame rate impact

## Future Enhancements

1. **Echo Cancellation**: Integrate speaker audio capture
2. **Voice Ducking**: Integrate game audio capture
3. **More Settings**: Threshold, makeup gain, etc.
4. **Presets**: Save/load processing configurations
5. **Visualization**: Real-time spectrum analyzer

## Conclusion

This fix transforms LethalMic from a basic audio level detector into a full-featured audio processing system. All UI settings now properly affect the audio quality, providing professional-grade voice processing for Lethal Company proximity chat. 