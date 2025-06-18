using System;

namespace Dissonance.Audio.Playback;

public struct SyncState
{
	public readonly TimeSpan ActualPlaybackPosition;

	public readonly TimeSpan IdealPlaybackPosition;

	public readonly TimeSpan Desync;

	public readonly float CompensatedPlaybackSpeed;

	public readonly bool Enabled;

	public SyncState(TimeSpan actualPlaybackPosition, TimeSpan idealPlaybackPosition, TimeSpan desync, float compensatedPlaybackSpeed, bool enabled)
	{
		this = default(SyncState);
		ActualPlaybackPosition = actualPlaybackPosition;
		IdealPlaybackPosition = idealPlaybackPosition;
		Desync = desync;
		CompensatedPlaybackSpeed = compensatedPlaybackSpeed;
		Enabled = enabled;
	}
}
