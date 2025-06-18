using System;
using System.Collections.Generic;
using System.ComponentModel;
using Dissonance.Config;
using Dissonance.Threading;
using JetBrains.Annotations;
using NAudio.Wave;
using UnityEngine;

namespace Dissonance.Audio.Capture;

internal class WebRtcPreprocessingPipeline : BasePreprocessingPipeline
{
	internal sealed class WebRtcPreprocessor : IDisposable
	{
		private readonly LockedValue<IntPtr> _handle;

		private readonly List<PropertyChangedEventHandler> _subscribed = new List<PropertyChangedEventHandler>();

		private readonly bool _useMobileAec;

		private NoiseSuppressionLevels _nsLevel;

		private VadSensitivityLevels _vadlevel;

		private AecSuppressionLevels _aecLevel;

		private AecmRoutingMode _aecmLevel;

		private NoiseSuppressionLevels NoiseSuppressionLevel
		{
			get
			{
				return _nsLevel;
			}
			set
			{
				using LockedValue<IntPtr>.Unlocker unlocker = _handle.Lock();
				_nsLevel = value;
				if (unlocker.Value != IntPtr.Zero)
				{
					AudioPluginDissonanceNative.Dissonance_ConfigureNoiseSuppression(unlocker.Value, _nsLevel);
				}
			}
		}

		private VadSensitivityLevels VadSensitivityLevel
		{
			get
			{
				return _vadlevel;
			}
			set
			{
				using LockedValue<IntPtr>.Unlocker unlocker = _handle.Lock();
				_vadlevel = value;
				if (unlocker.Value != IntPtr.Zero)
				{
					AudioPluginDissonanceNative.Dissonance_ConfigureVadSensitivity(unlocker.Value, _vadlevel);
				}
			}
		}

		private AecSuppressionLevels AecSuppressionLevel
		{
			get
			{
				return _aecLevel;
			}
			set
			{
				using LockedValue<IntPtr>.Unlocker unlocker = _handle.Lock();
				_aecLevel = value;
				if (!_useMobileAec && unlocker.Value != IntPtr.Zero)
				{
					AudioPluginDissonanceNative.Dissonance_ConfigureAecSuppression(unlocker.Value, _aecLevel, AecmRoutingMode.Disabled);
				}
			}
		}

		private AecmRoutingMode AecmSuppressionLevel
		{
			get
			{
				return _aecmLevel;
			}
			set
			{
				using LockedValue<IntPtr>.Unlocker unlocker = _handle.Lock();
				_aecmLevel = value;
				if (_useMobileAec && unlocker.Value != IntPtr.Zero)
				{
					AudioPluginDissonanceNative.Dissonance_ConfigureAecSuppression(unlocker.Value, AecSuppressionLevels.Disabled, _aecmLevel);
				}
			}
		}

		public WebRtcPreprocessor(bool useMobileAec)
		{
			_useMobileAec = useMobileAec;
			_handle = new LockedValue<IntPtr>(IntPtr.Zero);
			NoiseSuppressionLevel = VoiceSettings.Instance.DenoiseAmount;
			AecSuppressionLevel = VoiceSettings.Instance.AecSuppressionAmount;
			AecmSuppressionLevel = VoiceSettings.Instance.AecmRoutingMode;
			VadSensitivityLevel = VoiceSettings.Instance.VadSensitivity;
		}

		public bool Process(AudioPluginDissonanceNative.SampleRates inputSampleRate, float[] input, float[] output, int estimatedStreamDelay, bool isOutputMuted)
		{
			using LockedValue<IntPtr>.Unlocker unlocker = _handle.Lock();
			if (unlocker.Value == IntPtr.Zero)
			{
				throw Log.CreatePossibleBugException("Attempted  to access a null WebRtc Preprocessor encoder", "5C97EF6A-353B-4B96-871F-1073746B5708");
			}
			AudioPluginDissonanceNative.Dissonance_SetAgcIsOutputMutedState(unlocker.Value, isOutputMuted);
			AudioPluginDissonanceNative.ProcessorErrors processorErrors = AudioPluginDissonanceNative.Dissonance_PreprocessCaptureFrame(unlocker.Value, (int)inputSampleRate, input, output, estimatedStreamDelay);
			if (processorErrors != AudioPluginDissonanceNative.ProcessorErrors.Ok)
			{
				throw Log.CreatePossibleBugException($"Preprocessor error: '{processorErrors}'", "0A89A5E7-F527-4856-BA01-5A19578C6D88");
			}
			return AudioPluginDissonanceNative.Dissonance_GetVadSpeechState(unlocker.Value);
		}

		public void Reset()
		{
			using LockedValue<IntPtr>.Unlocker unlocker = _handle.Lock();
			if (unlocker.Value != IntPtr.Zero)
			{
				ClearFilterPreprocessor();
				AudioPluginDissonanceNative.Dissonance_DestroyPreprocessor(unlocker.Value);
				unlocker.Value = IntPtr.Zero;
			}
			unlocker.Value = CreatePreprocessor();
			SetFilterPreprocessor(unlocker.Value);
		}

		private IntPtr CreatePreprocessor()
		{
			VoiceSettings instance = VoiceSettings.Instance;
			AecSuppressionLevels aecLevel = AecSuppressionLevel;
			AecmRoutingMode aecmRoutingMode = AecmSuppressionLevel;
			if (_useMobileAec)
			{
				aecLevel = AecSuppressionLevels.Disabled;
			}
			else
			{
				aecmRoutingMode = AecmRoutingMode.Disabled;
			}
			IntPtr intPtr = AudioPluginDissonanceNative.Dissonance_CreatePreprocessor(NoiseSuppressionLevel, aecLevel, instance.AecDelayAgnostic, instance.AecExtendedFilter, instance.AecRefinedAdaptiveFilter, aecmRoutingMode, instance.AecmComfortNoise);
			AudioPluginDissonanceNative.Dissonance_ConfigureVadSensitivity(intPtr, instance.VadSensitivity);
			return intPtr;
		}

		private void SetFilterPreprocessor(IntPtr preprocessor)
		{
			using LockedValue<IntPtr>.Unlocker unlocker = _handle.Lock();
			if (unlocker.Value == IntPtr.Zero)
			{
				throw Log.CreatePossibleBugException("Attempted  to access a null WebRtc Preprocessor encoder", "3BA66D46-A7A6-41E8-BE38-52AFE5212ACD");
			}
			if (!AudioPluginDissonanceNative.Dissonance_PreprocessorExchangeInstance(IntPtr.Zero, unlocker.Value))
			{
				throw Log.CreatePossibleBugException("Cannot associate preprocessor with Playback filter - one already exists", "D5862DD2-B44E-4605-8D1C-29DD2C72A70C");
			}
			AudioPluginDissonanceNative.Dissonance_GetFilterState();
			Bind((VoiceSettings s) => s.DenoiseAmount, "DenoiseAmount", delegate(NoiseSuppressionLevels v)
			{
				NoiseSuppressionLevel = v;
			});
			Bind((VoiceSettings s) => s.AecSuppressionAmount, "AecSuppressionAmount", delegate(AecSuppressionLevels v)
			{
				AecSuppressionLevel = v;
			});
			Bind((VoiceSettings s) => s.AecmRoutingMode, "AecmRoutingMode", delegate(AecmRoutingMode v)
			{
				AecmSuppressionLevel = v;
			});
			Bind((VoiceSettings s) => s.VadSensitivity, "VadSensitivity", delegate(VadSensitivityLevels v)
			{
				VadSensitivityLevel = v;
			});
		}

		private void Bind<T>(Func<VoiceSettings, T> getValue, string propertyName, Action<T> setValue)
		{
			VoiceSettings settings = VoiceSettings.Instance;
			PropertyChangedEventHandler propertyChangedEventHandler;
			settings.PropertyChanged += (propertyChangedEventHandler = delegate(object sender, PropertyChangedEventArgs args)
			{
				if (args.PropertyName == propertyName)
				{
					setValue(getValue(settings));
				}
			});
			_subscribed.Add(propertyChangedEventHandler);
			propertyChangedEventHandler(settings, new PropertyChangedEventArgs(propertyName));
		}

		private bool ClearFilterPreprocessor(bool throwOnError = true)
		{
			using LockedValue<IntPtr>.Unlocker unlocker = _handle.Lock();
			if (unlocker.Value == IntPtr.Zero)
			{
				throw Log.CreatePossibleBugException("Attempted to access a null WebRtc Preprocessor encoder", "2DBC7779-F1B9-45F2-9372-3268FD8D7EBA");
			}
			if (!AudioPluginDissonanceNative.Dissonance_PreprocessorExchangeInstance(unlocker.Value, IntPtr.Zero))
			{
				if (throwOnError)
				{
					throw Log.CreatePossibleBugException("Cannot clear preprocessor from Playback filter. Editor restart required!", "6323106A-04BD-4217-9ECA-6FD49BF04FF0");
				}
				Log.Error("Failed to clear preprocessor from playback filter. Editor restart required!", "CBC6D727-BE07-4073-AA5A-F750A0CC023D");
				return false;
			}
			VoiceSettings instance = VoiceSettings.Instance;
			for (int i = 0; i < _subscribed.Count; i++)
			{
				instance.PropertyChanged -= _subscribed[i];
			}
			_subscribed.Clear();
			return true;
		}

		private void ReleaseUnmanagedResources()
		{
			using LockedValue<IntPtr>.Unlocker unlocker = _handle.Lock();
			if (unlocker.Value != IntPtr.Zero)
			{
				ClearFilterPreprocessor(throwOnError: false);
				AudioPluginDissonanceNative.Dissonance_DestroyPreprocessor(unlocker.Value);
				unlocker.Value = IntPtr.Zero;
			}
		}

		public void Dispose()
		{
			ReleaseUnmanagedResources();
			GC.SuppressFinalize(this);
		}

		~WebRtcPreprocessor()
		{
			ReleaseUnmanagedResources();
		}
	}

	internal sealed class RnnoisePreprocessor : IDisposable
	{
		private bool _enabled;

		private float _wetMix;

		private readonly LockedValue<IntPtr> _handle;

		private readonly List<PropertyChangedEventHandler> _subscribed = new List<PropertyChangedEventHandler>();

		private float[] _temp;

		private bool Enabled
		{
			get
			{
				return _enabled;
			}
			set
			{
				if (_enabled == value)
				{
					return;
				}
				using LockedValue<IntPtr>.Unlocker unlocker = _handle.Lock();
				if (value)
				{
					if (unlocker.Value == IntPtr.Zero)
					{
						unlocker.Value = AudioPluginDissonanceNative.Dissonance_CreateRnnoiseState();
					}
				}
				else if (unlocker.Value != IntPtr.Zero)
				{
					AudioPluginDissonanceNative.Dissonance_DestroyRnnoiseState(unlocker.Value);
					unlocker.Value = IntPtr.Zero;
				}
				_enabled = value;
			}
		}

		public RnnoisePreprocessor()
		{
			_handle = new LockedValue<IntPtr>(IntPtr.Zero);
			Bind((VoiceSettings v) => v.BackgroundSoundRemovalEnabled, "BackgroundSoundRemovalEnabled", delegate(bool a)
			{
				Enabled = a;
			});
			Bind((VoiceSettings v) => v.BackgroundSoundRemovalAmount, "BackgroundSoundRemovalAmount", delegate(float a)
			{
				_wetMix = a;
			});
		}

		private void Bind<T>(Func<VoiceSettings, T> getValue, string propertyName, Action<T> setValue)
		{
			VoiceSettings settings = VoiceSettings.Instance;
			PropertyChangedEventHandler propertyChangedEventHandler;
			settings.PropertyChanged += (propertyChangedEventHandler = delegate(object sender, PropertyChangedEventArgs args)
			{
				if (args.PropertyName == propertyName)
				{
					setValue(getValue(settings));
				}
			});
			_subscribed.Add(propertyChangedEventHandler);
			propertyChangedEventHandler(settings, new PropertyChangedEventArgs(propertyName));
		}

		public void Reset()
		{
			using LockedValue<IntPtr>.Unlocker unlocker = _handle.Lock();
			if (unlocker.Value != IntPtr.Zero)
			{
				AudioPluginDissonanceNative.Dissonance_DestroyRnnoiseState(unlocker.Value);
				unlocker.Value = IntPtr.Zero;
			}
			unlocker.Value = AudioPluginDissonanceNative.Dissonance_CreateRnnoiseState();
		}

		public void Process(AudioPluginDissonanceNative.SampleRates inputSampleRate, float[] input, float[] output)
		{
			if (Enabled)
			{
				using (LockedValue<IntPtr>.Unlocker unlocker = _handle.Lock())
				{
					if (unlocker.Value == IntPtr.Zero)
					{
						throw Log.CreatePossibleBugException("Attempted to access a null WebRtc Rnnoise", "1014ecad-f1cf-4377-a2cd-31e46df55b08");
					}
					if (_temp == null || _temp.Length != output.Length)
					{
						_temp = new float[output.Length];
					}
					if (!AudioPluginDissonanceNative.Dissonance_RnnoiseProcessFrame(unlocker.Value, input.Length, (int)inputSampleRate, input, _temp))
					{
						Log.Warn("Dissonance_RnnoiseProcessFrame returned false");
					}
					float wetMix = _wetMix;
					float num = 1f - wetMix;
					for (int i = 0; i < input.Length; i++)
					{
						output[i] = input[i] * num + _temp[i] * wetMix;
					}
					return;
				}
			}
			if (input != output)
			{
				Array.Copy(input, output, input.Length);
			}
		}

		public int GetGains(float[] output)
		{
			using LockedValue<IntPtr>.Unlocker unlocker = _handle.Lock();
			if (unlocker.Value == IntPtr.Zero)
			{
				return 0;
			}
			return AudioPluginDissonanceNative.Dissonance_RnnoiseGetGains(unlocker.Value, output, output.Length);
		}

		private void ReleaseUnmanagedResources()
		{
			using LockedValue<IntPtr>.Unlocker unlocker = _handle.Lock();
			if (unlocker.Value != IntPtr.Zero)
			{
				AudioPluginDissonanceNative.Dissonance_DestroyRnnoiseState(unlocker.Value);
				unlocker.Value = IntPtr.Zero;
			}
		}

		public void Dispose()
		{
			VoiceSettings instance = VoiceSettings.Instance;
			for (int i = 0; i < _subscribed.Count; i++)
			{
				instance.PropertyChanged -= _subscribed[i];
			}
			_subscribed.Clear();
			ReleaseUnmanagedResources();
			GC.SuppressFinalize(this);
		}

		~RnnoisePreprocessor()
		{
			ReleaseUnmanagedResources();
		}
	}

	private static readonly Log Log = Logs.Create(LogCategory.Recording, typeof(WebRtcPreprocessingPipeline).Name);

	private bool _isVadDetectingSpeech;

	private readonly bool _isMobilePlatform;

	private WebRtcPreprocessor _preprocessor;

	private RnnoisePreprocessor _rnnoise;

	private bool _isOutputMuted;

	protected override bool VadIsSpeechDetected => _isVadDetectingSpeech;

	public override bool IsOutputMuted
	{
		set
		{
			_isOutputMuted = value;
		}
	}

	public WebRtcPreprocessingPipeline([NotNull] WaveFormat inputFormat, bool mobilePlatform)
		: base(inputFormat, 480, 48000, 480, 48000)
	{
		_isMobilePlatform = mobilePlatform;
	}

	protected override void ThreadStart()
	{
		_preprocessor = new WebRtcPreprocessor(_isMobilePlatform);
		_rnnoise = new RnnoisePreprocessor();
		base.ThreadStart();
	}

	public override void Dispose()
	{
		base.Dispose();
		if (_preprocessor != null)
		{
			_preprocessor.Dispose();
		}
		if (_rnnoise != null)
		{
			_rnnoise.Dispose();
		}
	}

	protected override void ApplyReset()
	{
		if (_preprocessor != null)
		{
			_preprocessor.Reset();
		}
		if (_rnnoise != null)
		{
			_rnnoise.Reset();
		}
		base.ApplyReset();
	}

	protected override void PreprocessAudioFrame(float[] frame)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		AudioConfiguration configuration = AudioSettingsWatcher.Instance.Configuration;
		int preprocessorLatencyMs = base.PreprocessorLatencyMs;
		int num = (int)(1000f * ((float)configuration.dspBufferSize / (float)configuration.sampleRate));
		int estimatedStreamDelay = preprocessorLatencyMs + num;
		_rnnoise.Process(AudioPluginDissonanceNative.SampleRates.SampleRate48KHz, frame, frame);
		_isVadDetectingSpeech = _preprocessor.Process(AudioPluginDissonanceNative.SampleRates.SampleRate48KHz, frame, frame, estimatedStreamDelay, _isOutputMuted);
		SendSamplesToSubscribers(frame);
	}

	internal static AudioPluginDissonanceNative.FilterState GetAecFilterState()
	{
		return (AudioPluginDissonanceNative.FilterState)AudioPluginDissonanceNative.Dissonance_GetFilterState();
	}

	internal int GetBackgroundNoiseRemovalGains(float[] output)
	{
		return _rnnoise?.GetGains(output) ?? 0;
	}
}
