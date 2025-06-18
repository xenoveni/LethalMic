using System;
using JetBrains.Annotations;
using NAudio.Wave;

namespace Dissonance.Audio.Capture;

internal interface IFrameProvider
{
	[NotNull]
	WaveFormat WaveFormat { get; }

	uint FrameSize { get; }

	bool Read(ArraySegment<float> outBuffer);

	void Reset();
}
