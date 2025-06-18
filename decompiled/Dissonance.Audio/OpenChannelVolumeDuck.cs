using System;
using Dissonance.Audio.Playback;
using Dissonance.Config;

namespace Dissonance.Audio;

public class OpenChannelVolumeDuck : IVolumeProvider
{
	public const float FadeDurationSecondsDown = 0.3f;

	public const float FadeDurationSecondsUp = 0.5f;

	private readonly RoomChannels _rooms;

	private readonly PlayerChannels _players;

	private Fader _fader;

	public float TargetVolume => _fader.Volume;

	public OpenChannelVolumeDuck(RoomChannels rooms, PlayerChannels players)
	{
		_rooms = rooms;
		_players = players;
		_fader.FadeTo(1f, 0f);
	}

	public void Update(bool isMuted, float dt)
	{
		bool flag = !isMuted && (_rooms.Count > 0 || _players.Count > 0);
		float num = (flag ? VoiceSettings.Instance.VoiceDuckLevel : 1f);
		if (Math.Abs(_fader.EndVolume - num) > float.Epsilon)
		{
			_fader.FadeTo(num, flag ? 0.3f : 0.5f);
		}
		_fader.Update(dt);
	}
}
