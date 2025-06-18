using System;
using Dissonance.Audio.Codecs;
using NAudio.Wave;

namespace Dissonance.Audio.Playback;

internal struct FrameFormat : IEquatable<FrameFormat>
{
	public readonly Codec Codec;

	public readonly WaveFormat WaveFormat;

	public readonly uint FrameSize;

	public FrameFormat(Codec codec, WaveFormat waveFormat, uint frameSize)
	{
		Codec = codec;
		WaveFormat = waveFormat;
		FrameSize = frameSize;
	}

	public override int GetHashCode()
	{
		return (((103577 + (int)(Codec + 17)) * 101117 + WaveFormat.GetHashCode()) * 101117 + (int)FrameSize) * 101117;
	}

	public bool Equals(FrameFormat other)
	{
		if (Codec != other.Codec)
		{
			return false;
		}
		if (FrameSize != other.FrameSize)
		{
			return false;
		}
		if (!WaveFormat.Equals(other.WaveFormat))
		{
			return false;
		}
		return true;
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (obj is FrameFormat)
		{
			return Equals((FrameFormat)obj);
		}
		return false;
	}
}
