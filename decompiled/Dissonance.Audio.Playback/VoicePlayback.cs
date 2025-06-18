using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Dissonance.Networking;
using JetBrains.Annotations;
using NAudio.Wave;
using UnityEngine;

namespace Dissonance.Audio.Playback;

public class VoicePlayback : MonoBehaviour, IVoicePlaybackInternal, IRemoteChannelProvider, IVoicePlayback, IVolumeProvider
{
	[Serializable]
	[CompilerGenerated]
	private sealed class _003C_003Ec
	{
		public static readonly _003C_003Ec _003C_003E9 = new _003C_003Ec();

		public static PCMReaderCallback _003C_003E9__57_0;

		internal void _003COnEnable_003Eb__57_0(float[] buf)
		{
			for (int i = 0; i < buf.Length; i++)
			{
				buf[i] = 1f;
			}
		}
	}

	private static readonly Log Log = Logs.Create(LogCategory.Playback, "Voice Playback Component");

	private Transform _transformCache;

	private readonly SpeechSessionStream _sessions;

	private PlaybackOptions _cachedPlaybackOptions;

	private SamplePlaybackComponent _player;

	private CodecSettings _codecSettings;

	private FrameFormat _frameFormat;

	private float? _savedSpatialBlend;

	private Transform Transform
	{
		get
		{
			if ((Object)(object)_transformCache == (Object)null)
			{
				_transformCache = ((Component)this).transform;
			}
			return _transformCache;
		}
	}

	public AudioSource AudioSource { get; private set; }

	bool IVoicePlaybackInternal.AllowPositionalPlayback { get; set; }

	public bool IsActive => ((Behaviour)this).isActiveAndEnabled;

	public string PlayerName
	{
		get
		{
			return _sessions.PlayerName;
		}
		internal set
		{
			_sessions.PlayerName = value;
		}
	}

	public CodecSettings CodecSettings
	{
		get
		{
			return _codecSettings;
		}
		internal set
		{
			_codecSettings = value;
			if (_frameFormat.Codec != _codecSettings.Codec || _frameFormat.FrameSize != _codecSettings.FrameSize || _frameFormat.WaveFormat == null || _frameFormat.WaveFormat.SampleRate != _codecSettings.SampleRate)
			{
				_frameFormat = new FrameFormat(_codecSettings.Codec, new WaveFormat(_codecSettings.SampleRate, 1), _codecSettings.FrameSize);
			}
		}
	}

	public bool IsSpeaking
	{
		get
		{
			if ((Object)(object)_player != (Object)null)
			{
				return _player.HasActiveSession;
			}
			return false;
		}
	}

	public float Amplitude
	{
		get
		{
			if (!((Object)(object)_player == (Object)null))
			{
				return _player.ARV;
			}
			return 0f;
		}
	}

	public ChannelPriority Priority
	{
		get
		{
			if ((Object)(object)_player == (Object)null)
			{
				return ChannelPriority.None;
			}
			if (!_player.Session.HasValue)
			{
				return ChannelPriority.None;
			}
			return _cachedPlaybackOptions.Priority;
		}
	}

	bool IVoicePlaybackInternal.IsMuted { get; set; }

	float IVoicePlaybackInternal.PlaybackVolume { get; set; }

	private bool IsApplyingAudioSpatialization { get; set; }

	bool IVoicePlaybackInternal.IsApplyingAudioSpatialization => IsApplyingAudioSpatialization;

	internal IPriorityManager PriorityManager { get; set; }

	float? IVoicePlayback.PacketLoss
	{
		get
		{
			SpeechSession? session = _player.Session;
			if (!session.HasValue)
			{
				return null;
			}
			return session.Value.PacketLoss;
		}
	}

	float IVoicePlayback.Jitter => ((IJitterEstimator)_sessions).Jitter;

	[CanBeNull]
	internal IVolumeProvider VolumeProvider { get; set; }

	float IVolumeProvider.TargetVolume
	{
		get
		{
			if (((IVoicePlaybackInternal)this).IsMuted)
			{
				return 0f;
			}
			if (PriorityManager != null && PriorityManager.TopPriority > Priority)
			{
				return 0f;
			}
			float num = VolumeProvider?.TargetVolume ?? 1f;
			return ((IVoicePlaybackInternal)this).PlaybackVolume * num;
		}
	}

	public VoicePlayback()
	{
		_sessions = new SpeechSessionStream(this);
		((IVoicePlaybackInternal)this).PlaybackVolume = 1f;
	}

	public void Awake()
	{
		AudioSource = ((Component)this).GetComponent<AudioSource>();
		_player = ((Component)this).GetComponent<SamplePlaybackComponent>();
		((IVoicePlaybackInternal)this).Reset();
	}

	void IVoicePlaybackInternal.Reset()
	{
		((IVoicePlaybackInternal)this).IsMuted = false;
		((IVoicePlaybackInternal)this).PlaybackVolume = 1f;
	}

	public void OnEnable()
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Expected O, but got Unknown
		AudioSource.Stop();
		if (AudioSource.spatialize)
		{
			AudioSource.spatialize = false;
		}
		IsApplyingAudioSpatialization = true;
		AudioSource audioSource = AudioSource;
		int outputSampleRate = AudioSettings.outputSampleRate;
		object obj = _003C_003Ec._003C_003E9__57_0;
		if (obj == null)
		{
			PCMReaderCallback val = delegate(float[] buf)
			{
				for (int i = 0; i < buf.Length; i++)
				{
					buf[i] = 1f;
				}
			};
			_003C_003Ec._003C_003E9__57_0 = val;
			obj = (object)val;
		}
		audioSource.clip = AudioClip.Create("Flatline", 4096, 1, outputSampleRate, false, (PCMReaderCallback)obj);
		AudioSource.loop = true;
		AudioSource.pitch = 1f;
		AudioSource.dopplerLevel = 0f;
		AudioSource.mute = false;
		AudioSource.priority = 0;
	}

	public void OnDisable()
	{
		_sessions.StopSession(logNoSessionError: false);
		if ((Object)(object)AudioSource != (Object)null && (Object)(object)AudioSource.clip != (Object)null)
		{
			AudioClip clip = AudioSource.clip;
			AudioSource.clip = null;
			Object.Destroy((Object)(object)clip);
		}
	}

	public void Update()
	{
		if (!_player.HasActiveSession)
		{
			SpeechSession? speechSession = _sessions.TryDequeueSession();
			if (speechSession.HasValue)
			{
				_cachedPlaybackOptions = speechSession.Value.PlaybackOptions;
				_player.Play(speechSession.Value);
				AudioSource.Play();
			}
			else if (AudioSource.isPlaying)
			{
				AudioSource.Stop();
			}
		}
		if (AudioSource.mute)
		{
			Log.Warn("Voice AudioSource was muted, unmuting source. To mute a specific Dissonance player see: https://placeholder-software.co.uk/dissonance/docs/Reference/Other/VoicePlayerState.html#islocallymuted-bool");
			AudioSource.mute = false;
		}
		UpdatePositionalPlayback();
	}

	private void UpdatePositionalPlayback()
	{
		SpeechSession? session = _player.Session;
		if (!session.HasValue)
		{
			return;
		}
		_cachedPlaybackOptions = session.Value.PlaybackOptions;
		if (((IVoicePlaybackInternal)this).AllowPositionalPlayback && _cachedPlaybackOptions.IsPositional)
		{
			if (_savedSpatialBlend.HasValue)
			{
				AudioSource.spatialBlend = _savedSpatialBlend.Value;
				_savedSpatialBlend = null;
			}
		}
		else if (!_savedSpatialBlend.HasValue)
		{
			_savedSpatialBlend = AudioSource.spatialBlend;
			AudioSource.spatialBlend = 0f;
		}
	}

	void IVoicePlaybackInternal.SetTransform(Vector3 pos, Quaternion rot)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		Transform transform = Transform;
		transform.position = pos;
		transform.rotation = rot;
	}

	void IVoicePlaybackInternal.StartPlayback()
	{
		_sessions.StartSession(_frameFormat);
	}

	void IVoicePlaybackInternal.StopPlayback()
	{
		_sessions.StopSession();
	}

	void IVoicePlaybackInternal.ReceiveAudioPacket(VoicePacket packet)
	{
		_sessions.ReceiveFrame(packet);
	}

	public void ForceReset()
	{
		_sessions.ForceReset();
	}

	void IRemoteChannelProvider.GetRemoteChannels(List<RemoteChannel> output)
	{
		output.Clear();
		if (!((Object)(object)_player == (Object)null))
		{
			SpeechSession? session = _player.Session;
			if (session.HasValue)
			{
				session.Value.Channels.GetRemoteChannels(output);
			}
		}
	}
}
