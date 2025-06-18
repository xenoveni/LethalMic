using System;
using JetBrains.Annotations;
using NAudio.Wave;

namespace Dissonance.Audio.Playback;

internal interface IDecoderPipeline
{
	int BufferCount { get; }

	TimeSpan BufferTime { get; }

	float PacketLoss { get; }

	TimeSpan InputFrameTime { get; }

	PlaybackOptions PlaybackOptions { get; }

	[NotNull]
	WaveFormat OutputFormat { get; }

	SyncState SyncState { get; }

	void Prepare(SessionContext context);

	bool Read(ArraySegment<float> samples);

	void EnableDynamicSync();
}
