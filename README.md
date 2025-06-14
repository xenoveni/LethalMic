# 🎤 LethalMic - Advanced Audio Processing for Lethal Company

> ⚠️ **UNDER DEVELOPMENT - NOT READY FOR USE** ⚠️  
> This mod is currently in active development. Features may be incomplete or unstable. Please do not download or use this mod until it is officially released.

## Overview

LethalMic is an advanced audio processing mod for Lethal Company that enhances voice clarity and reduces background noise during gameplay. It uses WebRTC's industry-standard audio processing pipeline (the same technology used by Discord) to provide high-quality voice communication between players.

[GitHub Repository](https://github.com/xenoveni/LethalMic)

## How It Works

LethalMic integrates directly with Lethal Company's audio system to process microphone input in real-time. Here's how it works:

1. **Audio Capture**: The mod intercepts raw microphone input from the game's audio system
2. **WebRTC Processing**: Audio is processed through WebRTC's audio pipeline, which includes:
   - Noise suppression using spectral analysis
   - Automatic gain control for consistent volume
   - Voice activity detection to filter non-speech audio
   - Echo cancellation to prevent feedback
3. **Loop Prevention**: Advanced cross-correlation analysis detects and prevents audio feedback loops
4. **Output**: Processed audio is sent back to the game's voice chat system

## Features

### 1. WebRTC Audio Processing
- **Noise Suppression**: Advanced noise reduction using WebRTC's industry-standard algorithms
  - Removes background noise while preserving voice clarity
  - Adapts to different noise environments
  - Works with both constant and intermittent noise
- **Automatic Gain Control**: Maintains consistent voice levels
  - Prevents audio clipping
  - Boosts quiet speech
  - Reduces loud sounds
- **Voice Activity Detection**: Intelligently detects and processes only speech
  - Filters out background noise during silence
  - Smooth transitions between speech and silence
  - Configurable sensitivity
- **Echo Cancellation**: Prevents echo and feedback
  - Works with both speakers and headphones
  - Adapts to room acoustics
  - Real-time processing

### 2. Audio Loop Prevention
- Real-time detection of audio feedback loops
  - Cross-correlation analysis
  - Adaptive threshold detection
  - Minimal latency
- Automatic suppression of detected loops
  - Gradual suppression to prevent audio artifacts
  - Configurable suppression strength
  - Works with all audio devices

### 3. In-Game Toggles
All features can be toggled in-game through the BepInEx configuration menu:
- Enable/disable audio processing
- Toggle individual processing features
- Adjust loop detection settings
- Enable detailed logging for troubleshooting

## Configuration

The mod can be configured through the BepInEx configuration menu (accessible in-game):

```json
{
  "Audio Processing": {
    "Enable Audio Processing": true,
    "Noise Suppression": true,
    "Automatic Gain Control": true,
    "Voice Activity Detection": true,
    "Echo Cancellation": true,
    "Loop Detection": true,
    "Loop Detection Threshold": 0.3,
    "Loop Suppression Strength": 1.0
  },
  "Debug": {
    "Enable Detailed Logging": false
  }
}
```

### Default Settings
The mod is configured with Discord-like default settings for optimal voice quality:
- Noise suppression enabled
- Automatic gain control enabled
- Voice activity detection enabled
- Echo cancellation enabled
- Loop detection enabled with moderate sensitivity

## Performance

- **CPU Usage**: Low (1-3% depending on settings)
- **Memory Usage**: ~20MB
- **Latency**: < 10ms
- **Compatibility**: Windows 10/11

## Installation

1. Install [BepInEx](https://github.com/BepInEx/BepInEx/releases) for Lethal Company
2. Download the latest release of LethalMic
3. Extract the contents to your Lethal Company BepInEx plugins folder
4. Launch the game and configure settings through the BepInEx menu

## Dependencies

- BepInEx 5.4.x or later
- SIPSorcery.Media (included)
- Unity 2022.3.x or later

## Development

Built with:
- C# 9.0
- .NET Framework 4.7.2
- Unity 2022.3.x
- WebRTC audio processing pipeline

## License

MIT License - See LICENSE file for details

## Credits

- Original mod concept and implementation by xenoveni
- Built with BepInEx
- Uses Harmony for patching
- WebRTC audio processing by Google

## Support

For support, please:
1. Check the [Issues page](https://github.com/xenoveni/LethalMic/issues)
2. Join our Discord (Coming Soon)
3. Contact xenoveni on GitHub

---

_This mod is not affiliated with or endorsed by Zeekerss or the Lethal Company development team._ 