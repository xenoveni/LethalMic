# LethalMic Echo/Feedback Loop Fix Guide

## The Problem
Your microphone is picking up your friends' voices from your speakers and sending them back through your microphone, creating an echo loop. This is a common issue in Lethal Company.

## Root Causes
1. **Speaker audio bleeding into microphone** - Your speakers are too loud or too close to your mic
2. **Lethal Company's voice system** - The game's proximity chat doesn't have built-in echo cancellation
3. **Microphone sensitivity** - Your mic is too sensitive and picking up ambient audio

## Solutions Implemented

### 1. Adaptive Noise Gate
The mod now includes an **adaptive noise gate** that:
- Detects when your microphone is picking up speaker audio
- Automatically increases the noise gate threshold when echo is detected
- Prevents the echo from being transmitted

### 2. Disabled Echo Cancellation
- Temporarily disabled the echo canceller to prevent processing loops
- This prevents the system from creating additional echo

### 3. Reduced Logging Spam
- Only logs when settings actually change significantly
- Prevents log file bloat

## Immediate Fixes You Can Try

### 1. Hardware Solutions (Most Effective)
- **Use headphones instead of speakers** - This completely eliminates the echo
- **Move your microphone away from speakers** - Increase distance between mic and speakers
- **Lower speaker volume** - Reduce the volume so your mic doesn't pick it up
- **Use a directional microphone** - Point it away from your speakers

### 2. Windows Settings
- **Disable "Listen to this device"** in Windows microphone settings
- **Lower microphone boost** in Windows settings
- **Enable "Noise suppression"** in Windows microphone settings

### 3. Lethal Company Settings
- **Lower voice chat volume** in game settings
- **Use push-to-talk** instead of voice activity detection
- **Adjust proximity chat distance** if available

## Testing the Fix

### 1. Test with Headphones
1. **Connect headphones** to your computer
2. **Start Lethal Company** and join a game
3. **Test voice chat** - echo should be completely eliminated

### 2. Test with Speakers
1. **Lower speaker volume** significantly
2. **Move microphone** away from speakers
3. **Check the logs** for "Echo detected!" messages
4. **Adjust noise gate threshold** in the UI if needed

### 3. Monitor Logs
Look for these messages in `BepInEx/LogOutput.log`:
```
[Info   : LethalMic] Echo detected! RMS: 0.1234, using adaptive threshold: 0.0500
[Info   : LethalMic] Noise suppressor: Enabled=True, Strength=0.577
[Info   : LethalMic] Compressor: Enabled=True, Ratio=20, Attack=100ms, Release=1000ms
```

## Recommended Settings

### For Headphones:
- **Noise Gate**: Enabled, Threshold: 0.01-0.05
- **Compression**: Enabled, Ratio: 4:1, Attack: 10ms, Release: 100ms
- **Gain**: 1.0-2.0 (adjust based on your voice)

### For Speakers:
- **Noise Gate**: Enabled, Threshold: 0.05-0.15 (higher to prevent echo)
- **Compression**: Enabled, Ratio: 8:1, Attack: 20ms, Release: 200ms
- **Gain**: 0.5-1.0 (lower to reduce sensitivity)

## Troubleshooting

### Still Getting Echo?
1. **Check microphone placement** - Is it pointing away from speakers?
2. **Lower speaker volume** - Try 50% or lower
3. **Increase noise gate threshold** - Try 0.1 or higher
4. **Use push-to-talk** - Only transmit when you're actually speaking

### No Voice at All?
1. **Check microphone permissions** in Windows
2. **Verify microphone is selected** in Lethal Company
3. **Lower noise gate threshold** - Try 0.001
4. **Increase microphone gain** - Try 2.0 or higher

### Performance Issues?
1. **Disable unused processors** in the code
2. **Reduce buffer size** (currently 1024)
3. **Lower compression ratio** - Try 2:1 instead of 20:1

## Advanced Solutions

### 1. Use VoiceMeeter Banana
- Free virtual audio mixer
- Can route audio to prevent echo
- Provides additional echo cancellation

### 2. Use NVIDIA Broadcast
- AI-powered noise suppression
- Echo cancellation
- Works well with Lethal Company

### 3. Use Discord for Voice Chat
- Use Discord for voice communication
- Disable Lethal Company voice chat
- Discord has excellent echo cancellation

## Expected Results

After implementing these fixes:
- ✅ **No more echo loops** when using headphones
- ✅ **Reduced echo** when using speakers (with proper setup)
- ✅ **Better voice quality** with noise reduction and compression
- ✅ **Adaptive echo detection** that prevents feedback
- ✅ **Cleaner logs** without spam

## Final Recommendation

**Use headphones** - This is the most effective solution. The echo problem is fundamentally caused by speaker audio bleeding into your microphone, and headphones eliminate this completely while providing better audio quality.

The LethalMic mod will still provide excellent noise reduction, compression, and voice processing even with headphones! 