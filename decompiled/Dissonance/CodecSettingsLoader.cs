using Dissonance.Audio.Codecs;
using Dissonance.Audio.Codecs.Identity;
using Dissonance.Audio.Codecs.Opus;
using Dissonance.Config;
using JetBrains.Annotations;

namespace Dissonance;

internal class CodecSettingsLoader
{
	private static readonly Log Log = Logs.Create(LogCategory.Core, typeof(CodecSettingsLoader).Name);

	private bool _started;

	private bool _settingsReady;

	private readonly object _settingsWriteLock = new object();

	private CodecSettings _config;

	private AudioQuality _encoderQuality;

	private FrameSize _encoderFrameSize;

	private Codec _codec = Codec.Opus;

	private bool _encodeFec;

	public CodecSettings Config
	{
		get
		{
			GenerateSettings();
			return _config;
		}
	}

	public void Start(Codec codec = Codec.Opus)
	{
		_codec = codec;
		_encoderQuality = VoiceSettings.Instance.Quality;
		_encoderFrameSize = VoiceSettings.Instance.FrameSize;
		_encodeFec = VoiceSettings.Instance.ForwardErrorCorrection;
		_started = true;
	}

	private void GenerateSettings()
	{
		if (!_started)
		{
			throw Log.CreatePossibleBugException("Attempted to use codec settings before codec settings loaded", "9D4F1C1E-9C09-424A-86F7-B633E71EF100");
		}
		if (_settingsReady)
		{
			return;
		}
		lock (_settingsWriteLock)
		{
			if (!_settingsReady)
			{
				_config = GetEncoderSettings(_codec, _encoderQuality, _encoderFrameSize);
				_settingsReady = true;
			}
		}
	}

	private static CodecSettings GetEncoderSettings(Codec codec, AudioQuality quality, FrameSize frameSize)
	{
		return codec switch
		{
			Codec.Identity => new CodecSettings(Codec.Identity, 441u, 44100), 
			Codec.Opus => new CodecSettings(Codec.Opus, (uint)OpusEncoder.GetFrameSize(frameSize), 48000), 
			_ => throw Log.CreatePossibleBugException($"Unknown Codec {codec}", "6232F4FA-6993-49F9-AA79-2DBCF982FD8C"), 
		};
	}

	[NotNull]
	public IVoiceEncoder CreateEncoder()
	{
		if (!_started)
		{
			throw Log.CreatePossibleBugException("Attempted to use codec settings before codec settings loaded", "0BF71972-B96C-400B-B7D9-3E2AEE160470");
		}
		return _codec switch
		{
			Codec.Identity => new IdentityEncoder(44100, 441), 
			Codec.Opus => new OpusEncoder(_encoderQuality, _encoderFrameSize, _encodeFec), 
			_ => throw Log.CreatePossibleBugException($"Unknown Codec {_codec}", "6232F4FA-6993-49F9-AA79-2DBCF982FD8C"), 
		};
	}

	public override string ToString()
	{
		return Config.ToString();
	}
}
