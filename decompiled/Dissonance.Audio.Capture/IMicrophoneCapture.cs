using System;
using JetBrains.Annotations;
using NAudio.Wave;

namespace Dissonance.Audio.Capture;

public interface IMicrophoneCapture
{
	bool IsRecording { get; }

	[CanBeNull]
	string Device { get; }

	TimeSpan Latency { get; }

	[CanBeNull]
	WaveFormat StartCapture([CanBeNull] string name);

	void StopCapture();

	void Subscribe([NotNull] IMicrophoneSubscriber listener);

	bool Unsubscribe([NotNull] IMicrophoneSubscriber listener);

	bool UpdateSubscribers();
}
