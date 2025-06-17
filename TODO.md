# LethalMic Mod - Advanced Audio Processing To-Do

## Current State
- The mod currently uses a simple RMS threshold for voice detection in `LethalMicStatic.cs`.
- Advanced voice activity detection (VAD), noise suppression, adaptive noise floor estimation, and loop avoidance are implemented in separate classes:
  - `AINoiseSuppressionProcessor.VoiceActivityDetector`
  - `VoiceDuckingProcessor`
  - `AdaptiveNoiseFloorEstimator`
  - `FrequencyDomainLoopDetector`
- The UI displays audio levels and allows some configuration.
- There are buffer competition issues with Dissonance if both systems read the microphone.

## Best Approach
- **Intercept and process the microphone audio before it reaches Dissonance.**
- Apply advanced filters (noise cancellation, VAD, loop avoidance, compression, etc.) to the raw audio data.
- Feed the improved audio into Lethal Company's in-game voice chat.
- Use a single processing pipeline for all audio enhancements and detection.

## Step-by-Step Plan

### 1. Integrate Advanced VAD and Filters Into Main Pipeline
- Replace the simple RMS threshold in `LethalMicStatic` with the advanced VAD from `AINoiseSuppressionProcessor` or `VoiceDuckingProcessor`.
- Use the adaptive noise floor estimator for robust operation.

### 2. Build a Unified Audio Processing Pipeline
- Create a single processing function that:
  - Takes the raw mic buffer
  - Applies: noise suppression, VAD, loop avoidance, compression, etc.
  - Outputs the processed buffer for both in-game voice and the UI

### 3. Patch Dissonance's Input Pipeline (If Possible)
- Use Harmony to patch Dissonance's mic input method (e.g., `BasePreprocessingPipeline.Process` or similar) to use the processed buffer.
- If patching is not possible, continue using the custom mic reader, but minimize buffer competition.

### 4. UI and Settings
- Expose all filter settings in the UI (threshold, noise suppression strength, etc.).
- Allow real-time adjustment and show live feedback.

### 5. Testing and Tuning
- Test in various environments (quiet, noisy, with/without echo).
- Tune VAD and suppression parameters for best in-game experience.

---

## Concrete Action Plan

1. Refactor the main audio update loop to use the advanced VAD and noise suppression.
2. Integrate loop avoidance by running the `FrequencyDomainLoopDetector` on the buffer before sending to Dissonance.
3. (Optional, Advanced): Try again to patch Dissonance's input method with Harmony. Use reflection or DLL analysis if needed.
4. Update the UI to reflect the new detection and filtering logic.
5. Test and iterate until the mod is robust and high quality.

---

**We will now proceed to apply these steps one by one until the mod is complete.** 