using System;
using System.Runtime.InteropServices;

namespace Dissonance.Audio.Capture;

internal static class AudioPluginDissonanceNative
{
	public enum SampleRates
	{
		SampleRate8KHz = 8000,
		SampleRate16KHz = 16000,
		SampleRate32KHz = 32000,
		SampleRate48KHz = 48000
	}

	public enum ProcessorErrors
	{
		Ok = 0,
		Unspecified = -1,
		CreationFailed = -2,
		UnsupportedComponent = -3,
		UnsupportedFunction = -4,
		NullPointer = -5,
		BadParameter = -6,
		BadSampleRate = -7,
		BadDataLength = -8,
		BadNumberChannels = -9,
		FileError = -10,
		StreamParameterNotSet = -11,
		NotEnabled = -12
	}

	public enum FilterState
	{
		FilterNotRunning,
		FilterNoInstance,
		FilterNoSamplesSubmitted,
		FilterOk
	}

	private static readonly Log Log = Logs.Create(LogCategory.Core, "AudioPluginDissonanceNative");

	private const string ImportString = "AudioPluginDissonance";

	private const CallingConvention Convention = CallingConvention.Cdecl;

	internal static FilterState GetAecFilterState()
	{
		return (FilterState)Dissonance_GetFilterState();
	}

	[DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
	public static extern IntPtr Dissonance_CreateRnnoiseState();

	[DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
	public static extern void Dissonance_DestroyRnnoiseState(IntPtr state);

	[DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool Dissonance_RnnoiseProcessFrame(IntPtr state, int count, int sampleRate, float[] input, float[] output);

	[DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
	public static extern int Dissonance_RnnoiseGetGains(IntPtr state, float[] output, int length);

	[DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
	public static extern IntPtr Dissonance_CreatePreprocessor(NoiseSuppressionLevels nsLevel, AecSuppressionLevels aecLevel, bool aecDelayAgnostic, bool aecExtended, bool aecRefined, AecmRoutingMode aecmRoutingMode, bool aecmComfortNoise);

	[DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
	public static extern void Dissonance_DestroyPreprocessor(IntPtr handle);

	[DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
	public static extern void Dissonance_ConfigureNoiseSuppression(IntPtr handle, NoiseSuppressionLevels nsLevel);

	[DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
	public static extern void Dissonance_ConfigureVadSensitivity(IntPtr handle, VadSensitivityLevels nsLevel);

	[DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
	public static extern void Dissonance_ConfigureAecSuppression(IntPtr handle, AecSuppressionLevels aecLevel, AecmRoutingMode aecmRouting);

	[DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool Dissonance_GetVadSpeechState(IntPtr handle);

	[DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
	public static extern ProcessorErrors Dissonance_PreprocessCaptureFrame(IntPtr handle, int sampleRate, float[] input, float[] output, int streamDelay);

	[DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool Dissonance_PreprocessorExchangeInstance(IntPtr previous, IntPtr replacement);

	[DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
	public static extern int Dissonance_GetFilterState();

	[DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
	public static extern void Dissonance_GetAecMetrics(IntPtr floatBuffer, int bufferLength);

	[DllImport("AudioPluginDissonance", CallingConvention = CallingConvention.Cdecl)]
	public static extern void Dissonance_SetAgcIsOutputMutedState(IntPtr handle, bool isMuted);
}
