# LethalMic Testing Guide

## Quick Test After Fix

### 1. Build and Install
```bash
# Build the mod
dotnet build

# Copy the built DLL to your BepInEx/plugins folder
# The file should be: LethalMic.dll
```

### 2. Start Lethal Company and Check Logs

Look for these success messages in `BepInEx/LogOutput.log`:

```
[Info   : LethalMic] Initializing LethalMic static class...
[Info   : LethalMic] Configuration loaded - Enabled: True, Gain: 10
[Info   : LethalMic] Initializing audio system...
[Info   : LethalMic] Initializing StaticAudioManager...
[Info   : LethalMic] Audio buffers initialized with size: 1024
[Info   : LethalMic] Audio processors initialized successfully
[Info   : LethalMic] StaticAudioManager initialized with advanced processing pipeline
[Info   : LethalMic] Successfully initialized audio system with device: [Your Device]
```

### 3. Test UI Settings

1. **Open UI**: Press M key in game
2. **Test Gain**: Move the gain slider - you should see immediate changes in the level meter
3. **Test Noise Gate**: Toggle noise gate on/off - background noise should be reduced
4. **Test Compression**: Enable compression and adjust ratio - loud sounds should be compressed

### 4. Verify Settings Application

When you change settings, look for these log messages:

```
[Info   : LethalMic] Updating audio processor settings...
[Info   : LethalMic] Noise suppressor: Enabled=True, Strength=0.01
[Info   : LethalMic] Compressor: Enabled=True, Ratio=4, Attack=10ms, Release=100ms
[Info   : LethalMic] Audio processor settings updated successfully
```

### 5. Audio Quality Tests

- **Gain Test**: Speak at normal volume, then increase gain - your voice should get louder
- **Noise Gate Test**: Speak softly, then loudly - background noise should be reduced when speaking softly
- **Compression Test**: Speak with varying volumes - loud parts should be compressed, quiet parts should remain audible

## Troubleshooting

### If you see the original error:
```
[Error  : LethalMic] Error initializing audio system: System.InvalidCastException: Specified cast is not valid.
```

**Solution**: The fix didn't work. Check that you're using the updated files.

### If you see "No microphone devices found":
**Solution**: Check your Windows microphone settings and ensure a microphone is connected.

### If settings don't seem to work:
1. Check the logs for "Audio processor settings updated successfully"
2. Try restarting the game
3. Verify the UI is actually calling the setting methods

### If audio processing seems too intensive:
**Solution**: The processing pipeline is CPU-intensive. You can disable unused processors in the code if needed.

## Expected Results

After the fix:
- ✅ No more InvalidCastException
- ✅ UI settings immediately affect audio processing
- ✅ Better audio quality with noise reduction and compression
- ✅ Real-time processing without audio loops
- ✅ Professional-grade voice processing for Lethal Company

## Performance Notes

The audio processing pipeline is quite intensive. If you experience performance issues:
- Reduce buffer size (currently 1024)
- Disable unused processors
- Monitor CPU usage

The mod should provide significantly better voice quality for Lethal Company proximity chat! 