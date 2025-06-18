using System;

namespace NAudio.Wave;

public sealed class WaveFormat
{
	private readonly int _channels;

	private readonly int _sampleRate;

	public int Channels => _channels;

	public int SampleRate => _sampleRate;

	public WaveFormat(int sampleRate, int channels)
	{
		if (channels > 64)
		{
			throw new ArgumentOutOfRangeException("channels", "More than 64 channels");
		}
		_channels = channels;
		_sampleRate = sampleRate;
	}

	public bool Equals(WaveFormat other)
	{
		if (other.Channels == Channels)
		{
			return other.SampleRate == SampleRate;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return ((1022251 + _channels) * 16777619 + _sampleRate) * 16777619;
	}

	public override string ToString()
	{
		return $"(Channels:{Channels}, Rate:{SampleRate})";
	}
}
