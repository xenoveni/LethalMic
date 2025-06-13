# 🎤 LethalMic - Advanced Audio Processing for Lethal Company

> ⚠️ **UNDER DEVELOPMENT - NOT READY FOR USE** ⚠️  
> This mod is currently in active development. Features may be incomplete or unstable. Please do not download or use this mod until it is officially released.

## Overview

LethalMic is an advanced audio processing mod for Lethal Company that enhances voice clarity and reduces background noise during gameplay. It uses sophisticated signal processing techniques to improve communication quality between players.

## Features (In Development)

### 1. Dynamic Range Compression
- Makes quiet sounds more audible while preventing loud sounds from clipping
- Adjustable compression threshold and ratio
- Configurable attack and release times

### 2. FFT-based Noise Reduction
- Spectral subtraction for background noise removal
- Adjustable noise reduction strength
- Configurable FFT size for optimal performance

### 3. Echo Suppression
- Cross-correlation based echo detection and removal
- Adjustable suppression strength
- Real-time processing with minimal latency

### 4. Adaptive Equalization
- Automatic frequency response adjustment
- Enhances voice frequencies
- Reduces unwanted resonances

### 5. Spatial Enhancement
- Improves 3D audio positioning
- Enhances directional audio cues
- Better immersion in the game environment

## Technical Details

### Audio Processing Pipeline
1. **Input Processing**
   - Sample rate conversion
   - Channel management
   - Buffer initialization

2. **Noise Reduction**
   - FFT analysis
   - Noise floor estimation
   - Spectral subtraction
   - Inverse FFT

3. **Dynamic Processing**
   - RMS level detection
   - Gain calculation
   - Smooth gain application

4. **Spatial Processing**
   - Channel correlation
   - Phase alignment
   - Spatial enhancement

### Configuration Options
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

### Current Phase
- Core audio processing implementation
- Basic noise reduction
- Initial testing and optimization

### Planned Features
- [ ] Advanced noise profiling
- [ ] Machine learning-based voice enhancement
- [ ] Custom presets system
- [ ] Real-time visualization
- [ ] Performance optimization

### Known Issues
- High CPU usage with large FFT sizes
- Occasional audio artifacts during heavy processing
- Potential conflicts with other audio mods

## Installation (Coming Soon)

1. Install [BepInEx](https://github.com/BepInEx/BepInEx) for Lethal Company
2. Download the latest release from [GitHub Releases](https://github.com/xenoveni/LethalMic/releases)
3. Extract the contents to your Lethal Company BepInEx plugins folder
4. Configure settings in `BepInEx/config/com.xenoveni.lethalmic.cfg`

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Credits

- Original mod concept and implementation by xenoveni
- Built with [BepInEx](https://github.com/BepInEx/BepInEx)
- Uses [Harmony](https://github.com/pardeike/Harmony) for patching

## Support

For support, please:
1. Check the [Issues](https://github.com/xenoveni/LethalMic/issues) page
2. Join our [Discord](https://discord.gg/your-discord) (Coming Soon)
3. Contact xenoveni on GitHub

---

*This mod is not affiliated with or endorsed by Zeekerss or the Lethal Company development team.* 