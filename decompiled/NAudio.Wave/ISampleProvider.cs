using JetBrains.Annotations;

namespace NAudio.Wave;

internal interface ISampleProvider
{
	[NotNull]
	WaveFormat WaveFormat { get; }

	int Read([NotNull] float[] buffer, int offset, int count);
}
