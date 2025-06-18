using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dissonance.Audio.Playback;
using Dissonance.Networking;
using JetBrains.Annotations;
using UnityEngine;

namespace Dissonance;

internal class RemoteVoicePlayerState : VoicePlayerState
{
	private static readonly Log Log = Logs.Create(LogCategory.Core, typeof(RemoteVoicePlayerState).Name);

	private readonly IVoicePlaybackInternal _playback;

	private IDissonancePlayer _player;

	private static readonly ReadOnlyCollection<string> EmptyRoomsList = new ReadOnlyCollection<string>(new List<string>(0));

	private ReadOnlyCollection<string> _rooms;

	public override bool IsConnected
	{
		get
		{
			if (_playback.IsActive)
			{
				return _playback.PlayerName == base.Name;
			}
			return false;
		}
	}

	public override bool IsSpeaking
	{
		get
		{
			if (IsConnected)
			{
				return _playback.IsSpeaking;
			}
			return false;
		}
	}

	public override float Amplitude
	{
		get
		{
			if (!IsConnected)
			{
				return 0f;
			}
			return _playback.Amplitude;
		}
	}

	public override float Volume
	{
		get
		{
			return PlaybackInternal?.PlaybackVolume ?? 0f;
		}
		set
		{
			if (value < 0f || value > 1f)
			{
				throw new ArgumentOutOfRangeException("value", "Volume must be between 0 and 1");
			}
			IVoicePlaybackInternal playbackInternal = PlaybackInternal;
			if (playbackInternal != null)
			{
				playbackInternal.PlaybackVolume = value;
			}
		}
	}

	public override ChannelPriority? SpeakerPriority
	{
		get
		{
			IVoicePlaybackInternal playbackInternal = PlaybackInternal;
			if (playbackInternal != null && playbackInternal.IsSpeaking && !playbackInternal.IsMuted)
			{
				return playbackInternal.Priority;
			}
			return null;
		}
	}

	internal override IVoicePlaybackInternal PlaybackInternal
	{
		get
		{
			if (!IsConnected)
			{
				return null;
			}
			return _playback;
		}
	}

	public override bool IsLocallyMuted
	{
		get
		{
			if (IsConnected)
			{
				return _playback.IsMuted;
			}
			return false;
		}
		set
		{
			IVoicePlaybackInternal playbackInternal = PlaybackInternal;
			if (!IsConnected || playbackInternal == null)
			{
				Log.Warn("Attempted to (un)mute player {0}, but they are not connected", base.Name);
			}
			else
			{
				playbackInternal.IsMuted = value;
			}
		}
	}

	public override ReadOnlyCollection<string> Rooms => _rooms ?? EmptyRoomsList;

	public override IDissonancePlayer Tracker
	{
		get
		{
			return _player;
		}
		internal set
		{
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			_player = value;
			if (_playback.PlayerName == base.Name)
			{
				_playback.AllowPositionalPlayback = value != null;
				if (!_playback.AllowPositionalPlayback)
				{
					_playback.SetTransform(Vector3.zero, Quaternion.identity);
				}
			}
		}
	}

	public override float? PacketLoss => base.Playback?.PacketLoss;

	public override bool IsLocalPlayer => false;

	internal float? Jitter => base.Playback?.Jitter;

	internal RemoteVoicePlayerState([NotNull] IVoicePlaybackInternal playback)
		: base(playback.PlayerName)
	{
		_playback = playback;
		_playback.Reset();
	}

	internal override void Update()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		IVoicePlaybackInternal playbackInternal = PlaybackInternal;
		if (Tracker != null && playbackInternal != null && Tracker.IsTracking)
		{
			playbackInternal.SetTransform(Tracker.Position, Tracker.Rotation);
		}
	}

	public override void GetSpeakingChannels(List<RemoteChannel> channels)
	{
		channels.Clear();
		IVoicePlayback playback = base.Playback;
		if (playback != null)
		{
			((IRemoteChannelProvider)playback).GetRemoteChannels(channels);
		}
	}

	internal override void InvokeOnEnteredRoom(RoomEvent evtData)
	{
		_rooms = evtData.Rooms;
		base.InvokeOnEnteredRoom(evtData);
	}

	internal override void InvokeOnExitedRoom(RoomEvent evtData)
	{
		_rooms = evtData.Rooms;
		base.InvokeOnExitedRoom(evtData);
	}
}
