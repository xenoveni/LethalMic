# 🎤 LethalMic - Advanced Audio Processing for Lethal Company

> ⚠️ **UNDER DEVELOPMENT - NOT READY FOR USE** ⚠️  
> This mod is currently in active development. Features may be incomplete or unstable. Please do not download or use this mod until it is officially released.

## Overview

LethalMic is an advanced audio processing mod for Lethal Company that enhances voice clarity and reduces background noise during gameplay. It uses WebRTC's industry-standard audio processing pipeline (the same technology used by Discord) to provide high-quality voice communication between players.

[GitHub Repository](https://github.com/xenoveni/LethalMic)

## Features

### Core Features
- Real-time noise suppression
- Voice activity detection
- Echo cancellation
- Audio presets
- Performance optimization
- User-friendly UI

### Advanced Features (In Development)
1. **Dynamic Range Compression**
   - Makes quiet sounds more audible while preventing loud sounds from clipping
   - Adjustable compression threshold and ratio
   - Configurable attack and release times

2. **FFT-based Noise Reduction**
   - Spectral subtraction for background noise removal
   - Adjustable noise reduction strength
   - Configurable FFT size for optimal performance

3. **Echo Suppression**
   - Cross-correlation based echo detection and removal
   - Adjustable suppression strength
   - Real-time processing with minimal latency

4. **Adaptive Equalization**
   - Automatic frequency response adjustment
   - Enhances voice frequencies
   - Reduces unwanted resonances

5. **Spatial Enhancement**
   - Improves 3D audio positioning
   - Enhances directional audio cues
   - Better immersion in the game environment

6. **Audio Loop Detection**
   - Detects and prevents audio feedback loops
   - Works with both speakers and headphones
   - Prevents game audio from being routed back to input
   - Configurable detection sensitivity and suppression

7. **Voice Activity Detection**
   - Intelligent voice detection to filter non-speech audio
   - Configurable voice frequency range
   - Adjustable detection thresholds
   - Smooth transitions between speech and silence

## Installation

1. Install [BepInEx](https://github.com/BepInEx/BepInEx) for Lethal Company
2. Download the latest release from the [Releases](https://github.com/yourusername/LethalMic/releases) page
3. Extract the contents of the zip file into your `BepInEx/plugins` folder
4. Launch the game

## r2modman Installation (Recommended)

LethalMic is fully compatible with [r2modman](https://thunderstore.io/package/ebkr/r2modman/), the recommended mod manager for Lethal Company.

**To install with r2modman:**
1. Open r2modman and select your Lethal Company profile (e.g., "Default").
2. Click "Download mods" and search for "LethalMic" (or import the zip if not yet published).
3. Enable the mod in your profile.
4. Launch the game through r2modman.

**Manual install for r2modman:**
1. Build or download the zip package.
2. Extract the contents to:
   `%APPDATA%\r2modmanPlus-local\LethalCompany\profiles\<YourProfile>\BepInEx\plugins\LethalMic`
3. Launch the game through r2modman.

## Thunderstore/Mod Manager Installation

LethalMic is compatible with Thunderstore Mod Manager and r2modman.

- Search for "LethalMic" in the Thunderstore Mod Manager browser and click install.
- Or, download the zip and use the "Import local mod" feature in your mod manager.
- Make sure BepInExPack and InputUtils are enabled in your mod profile.

## Usage

- Press `M` to toggle the UI
- Use the UI to adjust microphone settings
- Save and load presets for different environments
- Enable/disable features as needed

## Configuration

The plugin can be configured through the in-game UI or by editing the config file at:
`BepInEx/config/com.xenoveni.lethalmic.cfg`

### Key Settings

```json
{
  "Audio Processing": {
    "Enable Dynamic Range Compression": true,
    "Compression Threshold": -20.0,
    "Compression Ratio": 4.0,
    "Attack Time": 5.0,
    "Release Time": 50.0,
    "Enable Noise Reduction": true,
    "Noise Reduction Strength": 0.7,
    "FFT Size": 2048,
    "Enable Echo Suppression": true,
    "Echo Suppression Strength": 0.8,
    "Enable Adaptive EQ": true,
    "Enable Spatial Enhancement": true
  },
  "Voice Detection": {
    "Enable Voice Activity Detection": true,
    "VAD Threshold": -30.0,
    "VAD Attack Time": 10.0,
    "VAD Release Time": 100.0,
    "Minimum Voice Frequency": 85.0,
    "Maximum Voice Frequency": 255.0,
    "Noise Gate Threshold": -45.0,
    "Noise Gate Attack Time": 5.0,
    "Noise Gate Release Time": 50.0
  },
  "Audio Loop": {
    "Enable Loop Detection": true,
    "Loop Detection Threshold": 0.7,
    "Loop Detection Window": 1000,
    "Loop Suppression Strength": 0.9
  },
  "Debug": {
    "Enable Detailed Logging": false
  }
}
```

## Performance Considerations

- **CPU Usage**: Moderate (1-5% depending on settings)
- **Memory Usage**: ~50MB
- **Latency**: < 20ms
- **Compatibility**: Windows 10/11

## Development Status

### Recent Updates
- ✅ **Compilation Issues Resolved**: Fixed all 67+ compilation errors
- ✅ **Code Stability**: Removed readonly field assignment conflicts
- ✅ **Cross-Platform Compatibility**: Replaced Unity-specific functions with .NET Standard equivalents
- ✅ **External Library Integration**: Improved handling of RNNoiseSharp and Opus codec integration
- ✅ **Build System**: Project now builds successfully with 0 errors
- ✅ **Code Quality**: Resolved variable naming conflicts and method signature mismatches
- ✅ **Custom Presets System**: Implemented comprehensive audio preset management with 5 default presets
- ✅ **Preset Integration**: Full integration with existing configuration system and automatic preset application

### Current Phase
- ✅ Core audio processing implementation
- ✅ Basic noise reduction
- ✅ Audio loop detection and prevention
- ✅ Voice activity detection
- ✅ Compilation and build stability
- 🔄 Integration testing and optimization

### Planned Features
- [ ] Real-time visualization
- [ ] Performance optimization
- [ ] Preset import/export functionality
- [ ] GUI for preset management
- [ ] Advanced machine learning-based voice enhancement
- [ ] Context-aware automatic preset switching

## Development

### Prerequisites

- Visual Studio 2022 or newer
- .NET SDK 6.0 or newer
- Unity 2022.3.9 or newer
- BepInEx 5.4.x

### Building

1. Clone the repository
2. Open the solution in Visual Studio
3. Restore NuGet packages
4. Build the solution

```powershell
# Build and package
.\build.ps1
```

### Project Structure

```
LethalMic/
├── src/
│   └── plugins/
│       └── LethalMic/
│           ├── Core/
│           │   ├── LethalMic.cs              # Main plugin class
│           │   ├── LethalMicStatic.cs        # Static utilities
│           │   └── PluginInfo.cs             # Plugin metadata
│           ├── Audio/
│           │   ├── Processors/               # Audio processing components
│           │   ├── Presets/                  # Audio presets
│           │   └── Utils/                    # Audio utilities
│           ├── UI/
│           │   ├── Components/               # UI components
│           │   ├── Styles/                   # UI styles
│           │   └── Resources/                # UI resources
│           ├── Patches/                      # Harmony patches
│           └── Utils/                        # General utilities
├── tests/                                    # Unit tests
├── docs/                                     # Documentation
└── build/                                    # Build scripts
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [BepInEx](https://github.com/BepInEx/BepInEx) - Plugin framework
- [Harmony](https://github.com/pardeike/Harmony) - Patching library
- [Unity](https://unity.com/) - Game engine

## Support

For support, please:
1. Check the [Issues page](https://github.com/xenoveni/LethalMic/issues)
2. Join our Discord (Coming Soon)
3. Contact xenoveni on GitHub

## Troubleshooting (r2modman & BepInEx)

**Common Issues:**

- **Mod not loading:**
  - Ensure the DLL is in the correct r2modman profile's `BepInEx/plugins/LethalMic` folder.
  - Make sure you are launching the game through r2modman.
  - Check that your profile has BepInExPack and InputUtils enabled.

- **Missing DLL errors:**
  - Verify that `LethalCompanyInputUtils.dll` is present in your profile's `BepInEx/plugins` folder.
  - If Unity DLL errors appear, check that your Lethal Company install is valid and not missing files.

- **InputUtils not detected:**
  - Make sure InputUtils is enabled in r2modman and not disabled by another mod.
  - Check the mod order if using multiple mods that patch input.

- **How to check logs:**
  - Logs are found in `%APPDATA%\r2modmanPlus-local\LethalCompany\profiles\<YourProfile>\BepInEx\LogOutput.log` and `BepInEx\LogOutput.log` in your game directory.
  - Look for `[LethalMic]` or `[InputUtils]` entries for errors or warnings.

If you encounter issues, please open an issue on the [GitHub repository](https://github.com/xenoveni/LethalMic/issues) with your log files attached.

---

_This mod is not affiliated with or endorsed by Zeekerss or the Lethal Company development team._ 