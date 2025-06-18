using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Dissonance.Audio.Capture;
using JetBrains.Annotations;
using UnityEngine;

namespace Dissonance.Config;

public sealed class VoiceSettings : ScriptableObject, INotifyPropertyChanged
{
	private static readonly Log Log = Logs.Create(LogCategory.Recording, typeof(VoiceSettings).Name);

	private const string PersistName_Quality = "Dissonance_Audio_Quality";

	private const string PersistName_FrameSize = "Dissonance_Audio_FrameSize";

	private const string PersistName_Fec = "Dissonance_Audio_DisableFEC";

	private const string PersistName_DenoiseAmount = "Dissonance_Audio_Denoise_Amount";

	private const string PersistName_PttDuckAmount = "Dissonance_Audio_Duck_Amount";

	private const string PersistName_VadSensitivity = "Dissonance_Audio_Vad_Sensitivity";

	private const string PersistName_BgDenoiseEnabled = "Dissonance_Audio_BgDenoise_Enabled";

	private const string PersistName_BgDenoiseWetmix = "Dissonance_Audio_BgDenoise_Amount";

	private const string PersistName_AecSuppressionAmount = "Dissonance_Audio_Aec_Suppression_Amount";

	private const string PersistName_AecDelayAgnostic = "Dissonance_Audio_Aec_Delay_Agnostic";

	private const string PersistName_AecExtendedFilter = "Dissonance_Audio_Aec_Extended_Filter";

	private const string PersistName_AecRefinedAdaptiveFilter = "Dissonance_Audio_Aec_Refined_Adaptive_Filter";

	private const string PersistName_AecmRoutingMode = "Dissonance_Audio_Aecm_Routing_Mode";

	private const string PersistName_AecmComfortNoise = "Dissonance_Audio_Aecm_Comfort_Noise";

	private const string SettingsFileResourceName = "VoiceSettings";

	public static readonly string SettingsFilePath = Path.Combine("Assets/Plugins/Dissonance/Resources", "VoiceSettings.asset");

	[SerializeField]
	private AudioQuality _quality;

	[SerializeField]
	private FrameSize _frameSize;

	[SerializeField]
	private int _forwardErrorCorrection;

	[SerializeField]
	private int _denoiseAmount;

	[SerializeField]
	private int _bgSoundRemovalEnabled;

	[SerializeField]
	private float _bgSoundRemovalAmount;

	[SerializeField]
	private int _vadSensitivity;

	[SerializeField]
	private int _aecAmount;

	[SerializeField]
	private int _aecDelayAgnostic;

	[SerializeField]
	private int _aecExtendedFilter;

	[SerializeField]
	private int _aecRefinedAdaptiveFilter;

	[SerializeField]
	private int _aecmRoutingMode;

	[SerializeField]
	private int _aecmComfortNoise;

	[SerializeField]
	private float _voiceDuckLevel;

	private static VoiceSettings _instance;

	public AudioQuality Quality
	{
		get
		{
			return _quality;
		}
		set
		{
			Preferences.Set("Dissonance_Audio_Quality", ref _quality, value, delegate(string key, AudioQuality q)
			{
				PlayerPrefs.SetInt(key, (int)q);
			}, Log);
			OnPropertyChanged("Quality");
		}
	}

	public FrameSize FrameSize
	{
		get
		{
			return _frameSize;
		}
		set
		{
			Preferences.Set("Dissonance_Audio_FrameSize", ref _frameSize, value, delegate(string key, FrameSize f)
			{
				PlayerPrefs.SetInt(key, (int)f);
			}, Log);
			OnPropertyChanged("FrameSize");
		}
	}

	public bool ForwardErrorCorrection
	{
		get
		{
			return Convert.ToBoolean(_forwardErrorCorrection);
		}
		set
		{
			Preferences.Set("Dissonance_Audio_DisableFEC", ref _forwardErrorCorrection, Convert.ToInt32(value), (Action<string, int>)PlayerPrefs.SetInt, Log, (IEqualityComparer<int>)null, setAtRuntime: true);
			OnPropertyChanged("ForwardErrorCorrection");
		}
	}

	public NoiseSuppressionLevels DenoiseAmount
	{
		get
		{
			return (NoiseSuppressionLevels)_denoiseAmount;
		}
		set
		{
			Preferences.Set("Dissonance_Audio_Denoise_Amount", ref _denoiseAmount, (int)value, (Action<string, int>)PlayerPrefs.SetInt, Log, (IEqualityComparer<int>)null, setAtRuntime: true);
			OnPropertyChanged("DenoiseAmount");
		}
	}

	public bool BackgroundSoundRemovalEnabled
	{
		get
		{
			return Convert.ToBoolean(_bgSoundRemovalEnabled);
		}
		set
		{
			Preferences.Set("Dissonance_Audio_BgDenoise_Enabled", ref _bgSoundRemovalEnabled, Convert.ToInt32(value), (Action<string, int>)PlayerPrefs.SetInt, Log, (IEqualityComparer<int>)null, setAtRuntime: true);
			OnPropertyChanged("BackgroundSoundRemovalEnabled");
		}
	}

	public float BackgroundSoundRemovalAmount
	{
		get
		{
			return _bgSoundRemovalAmount;
		}
		set
		{
			Preferences.Set("Dissonance_Audio_BgDenoise_Amount", ref _bgSoundRemovalAmount, Mathf.Clamp01(value), (Action<string, float>)PlayerPrefs.SetFloat, Log, (IEqualityComparer<float>)null, setAtRuntime: true);
			OnPropertyChanged("BackgroundSoundRemovalAmount");
		}
	}

	public VadSensitivityLevels VadSensitivity
	{
		get
		{
			return (VadSensitivityLevels)_vadSensitivity;
		}
		set
		{
			Preferences.Set("Dissonance_Audio_Vad_Sensitivity", ref _vadSensitivity, (int)value, (Action<string, int>)PlayerPrefs.SetInt, Log, (IEqualityComparer<int>)null, setAtRuntime: true);
			OnPropertyChanged("VadSensitivity");
		}
	}

	public AecSuppressionLevels AecSuppressionAmount
	{
		get
		{
			return (AecSuppressionLevels)_aecAmount;
		}
		set
		{
			Preferences.Set("Dissonance_Audio_Aec_Suppression_Amount", ref _aecAmount, (int)value, (Action<string, int>)PlayerPrefs.SetInt, Log, (IEqualityComparer<int>)null, setAtRuntime: true);
			OnPropertyChanged("AecSuppressionAmount");
		}
	}

	public bool AecDelayAgnostic
	{
		get
		{
			return Convert.ToBoolean(_aecDelayAgnostic);
		}
		set
		{
			Preferences.Set("Dissonance_Audio_Aec_Delay_Agnostic", ref _aecDelayAgnostic, Convert.ToInt32(value), (Action<string, int>)PlayerPrefs.SetInt, Log, (IEqualityComparer<int>)null, setAtRuntime: true);
			OnPropertyChanged("AecDelayAgnostic");
		}
	}

	public bool AecExtendedFilter
	{
		get
		{
			return Convert.ToBoolean(_aecExtendedFilter);
		}
		set
		{
			Preferences.Set("Dissonance_Audio_Aec_Extended_Filter", ref _aecExtendedFilter, Convert.ToInt32(value), (Action<string, int>)PlayerPrefs.SetInt, Log, (IEqualityComparer<int>)null, setAtRuntime: true);
			OnPropertyChanged("AecExtendedFilter");
		}
	}

	public bool AecRefinedAdaptiveFilter
	{
		get
		{
			return Convert.ToBoolean(_aecRefinedAdaptiveFilter);
		}
		set
		{
			Preferences.Set("Dissonance_Audio_Aec_Refined_Adaptive_Filter", ref _aecRefinedAdaptiveFilter, Convert.ToInt32(value), (Action<string, int>)PlayerPrefs.SetInt, Log, (IEqualityComparer<int>)null, setAtRuntime: true);
			OnPropertyChanged("AecRefinedAdaptiveFilter");
		}
	}

	public AecmRoutingMode AecmRoutingMode
	{
		get
		{
			return (AecmRoutingMode)_aecmRoutingMode;
		}
		set
		{
			Preferences.Set("Dissonance_Audio_Aecm_Routing_Mode", ref _aecmRoutingMode, (int)value, (Action<string, int>)PlayerPrefs.SetInt, Log, (IEqualityComparer<int>)null, setAtRuntime: true);
			OnPropertyChanged("AecmRoutingMode");
		}
	}

	public bool AecmComfortNoise
	{
		get
		{
			return Convert.ToBoolean(_aecmComfortNoise);
		}
		set
		{
			Preferences.Set("Dissonance_Audio_Aecm_Comfort_Noise", ref _aecmComfortNoise, Convert.ToInt32(value), (Action<string, int>)PlayerPrefs.SetInt, Log, (IEqualityComparer<int>)null, setAtRuntime: true);
			OnPropertyChanged("AecmComfortNoise");
		}
	}

	public float VoiceDuckLevel
	{
		get
		{
			return _voiceDuckLevel;
		}
		set
		{
			Preferences.Set("Dissonance_Audio_Duck_Amount", ref _voiceDuckLevel, value, (Action<string, float>)PlayerPrefs.SetFloat, Log, (IEqualityComparer<float>)null, setAtRuntime: true);
			OnPropertyChanged("VoiceDuckLevel");
		}
	}

	[NotNull]
	public static VoiceSettings Instance
	{
		get
		{
			if ((Object)(object)_instance == (Object)null)
			{
				_instance = Load();
			}
			return _instance;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	[NotifyPropertyChangedInvocator]
	private void OnPropertyChanged(string propertyName)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public VoiceSettings()
	{
		LoadDefaults();
	}

	private void LoadDefaults()
	{
		_quality = AudioQuality.Medium;
		_frameSize = FrameSize.Medium;
		_forwardErrorCorrection = Convert.ToInt32(value: true);
		_denoiseAmount = 2;
		_vadSensitivity = 1;
		_bgSoundRemovalEnabled = Convert.ToInt32(value: false);
		_bgSoundRemovalAmount = 0.65f;
		_aecAmount = -1;
		_aecDelayAgnostic = Convert.ToInt32(value: true);
		_aecExtendedFilter = Convert.ToInt32(value: true);
		_aecRefinedAdaptiveFilter = Convert.ToInt32(value: true);
		_aecmRoutingMode = -1;
		_aecmComfortNoise = Convert.ToInt32(value: true);
		_voiceDuckLevel = 0.5f;
	}

	public void Reset()
	{
		PlayerPrefs.DeleteKey("Dissonance_Audio_Quality");
		PlayerPrefs.DeleteKey("Dissonance_Audio_FrameSize");
		PlayerPrefs.DeleteKey("Dissonance_Audio_DisableFEC");
		PlayerPrefs.DeleteKey("Dissonance_Audio_Denoise_Amount");
		PlayerPrefs.DeleteKey("Dissonance_Audio_Vad_Sensitivity");
		PlayerPrefs.DeleteKey("Dissonance_Audio_BgDenoise_Enabled");
		PlayerPrefs.DeleteKey("Dissonance_Audio_BgDenoise_Amount");
		PlayerPrefs.DeleteKey("Dissonance_Audio_Aec_Suppression_Amount");
		PlayerPrefs.DeleteKey("Dissonance_Audio_Aec_Delay_Agnostic");
		PlayerPrefs.DeleteKey("Dissonance_Audio_Aec_Extended_Filter");
		PlayerPrefs.DeleteKey("Dissonance_Audio_Aec_Refined_Adaptive_Filter");
		PlayerPrefs.DeleteKey("Dissonance_Audio_Aecm_Routing_Mode");
		PlayerPrefs.DeleteKey("Dissonance_Audio_Aecm_Comfort_Noise");
		PlayerPrefs.DeleteKey("Dissonance_Audio_Duck_Amount");
		LoadDefaults();
	}

	public static void Preload()
	{
		if ((Object)(object)_instance == (Object)null)
		{
			_instance = Load();
		}
	}

	[NotNull]
	private static VoiceSettings Load()
	{
		VoiceSettings voiceSettings = Resources.Load<VoiceSettings>("VoiceSettings");
		if ((Object)(object)voiceSettings == (Object)null)
		{
			voiceSettings = ScriptableObject.CreateInstance<VoiceSettings>();
		}
		Preferences.Get("Dissonance_Audio_Quality", ref voiceSettings._quality, (string s, AudioQuality q) => (AudioQuality)PlayerPrefs.GetInt(s, (int)q), Log);
		Preferences.Get("Dissonance_Audio_FrameSize", ref voiceSettings._frameSize, (string s, FrameSize f) => (FrameSize)PlayerPrefs.GetInt(s, (int)f), Log);
		Preferences.Get("Dissonance_Audio_DisableFEC", ref voiceSettings._forwardErrorCorrection, (Func<string, int, int>)PlayerPrefs.GetInt, Log);
		Preferences.Get("Dissonance_Audio_Denoise_Amount", ref voiceSettings._denoiseAmount, (Func<string, int, int>)PlayerPrefs.GetInt, Log);
		Preferences.Get("Dissonance_Audio_Vad_Sensitivity", ref voiceSettings._vadSensitivity, (Func<string, int, int>)PlayerPrefs.GetInt, Log);
		Preferences.Get("Dissonance_Audio_BgDenoise_Enabled", ref voiceSettings._bgSoundRemovalEnabled, (Func<string, int, int>)PlayerPrefs.GetInt, Log);
		Preferences.Get("Dissonance_Audio_BgDenoise_Amount", ref voiceSettings._bgSoundRemovalAmount, (Func<string, float, float>)PlayerPrefs.GetFloat, Log);
		Preferences.Get("Dissonance_Audio_Aec_Suppression_Amount", ref voiceSettings._aecAmount, (Func<string, int, int>)PlayerPrefs.GetInt, Log);
		Preferences.Get("Dissonance_Audio_Aec_Delay_Agnostic", ref voiceSettings._aecDelayAgnostic, (Func<string, int, int>)PlayerPrefs.GetInt, Log);
		Preferences.Get("Dissonance_Audio_Aec_Extended_Filter", ref voiceSettings._aecExtendedFilter, (Func<string, int, int>)PlayerPrefs.GetInt, Log);
		Preferences.Get("Dissonance_Audio_Aec_Refined_Adaptive_Filter", ref voiceSettings._aecRefinedAdaptiveFilter, (Func<string, int, int>)PlayerPrefs.GetInt, Log);
		Preferences.Get("Dissonance_Audio_Aecm_Routing_Mode", ref voiceSettings._aecmRoutingMode, (Func<string, int, int>)PlayerPrefs.GetInt, Log);
		Preferences.Get("Dissonance_Audio_Aecm_Comfort_Noise", ref voiceSettings._aecmRoutingMode, (Func<string, int, int>)PlayerPrefs.GetInt, Log);
		Preferences.Get("Dissonance_Audio_Duck_Amount", ref voiceSettings._voiceDuckLevel, (Func<string, float, float>)PlayerPrefs.GetFloat, Log);
		return voiceSettings;
	}

	public override string ToString()
	{
		return $"Quality: {Quality}, FrameSize: {FrameSize}, FEC: {ForwardErrorCorrection}, DenoiseAmount: {DenoiseAmount}, RNN: {BackgroundSoundRemovalEnabled} ({BackgroundSoundRemovalAmount:0.0#}) VoiceDuckLevel: {VoiceDuckLevel} VAD: {VadSensitivity}";
	}
}
