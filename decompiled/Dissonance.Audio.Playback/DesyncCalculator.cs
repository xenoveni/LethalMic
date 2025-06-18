using System;
using UnityEngine;

namespace Dissonance.Audio.Playback;

internal struct DesyncCalculator
{
	private const int MaxAllowedDesyncMillis = 1000;

	private const float MaximumPlaybackAdjustment = 0.15f;

	internal int DesyncMilliseconds { get; private set; }

	internal float CorrectedPlaybackSpeed => CalculateCorrectionFactor(DesyncMilliseconds);

	internal void Update(TimeSpan ideal, TimeSpan actual)
	{
		DesyncMilliseconds = CalculateDesync(ideal, actual);
	}

	internal void Skip(int deltaDesyncMilliseconds)
	{
		DesyncMilliseconds += deltaDesyncMilliseconds;
	}

	private static int CalculateDesync(TimeSpan idealPlaybackPosition, TimeSpan actualPlaybackPosition)
	{
		TimeSpan timeSpan = idealPlaybackPosition - actualPlaybackPosition;
		if (timeSpan.TotalMilliseconds > 29.0)
		{
			return (int)timeSpan.TotalMilliseconds - 29;
		}
		if (timeSpan.TotalMilliseconds < -29.0)
		{
			return (int)timeSpan.TotalMilliseconds + 29;
		}
		return 0;
	}

	private static float CalculateCorrectionFactor(float desyncMilliseconds)
	{
		float num = Mathf.Clamp(desyncMilliseconds / 1000f, -1f, 1f);
		return 1f + Mathf.LerpUnclamped(0f, 0.15f, num);
	}
}
