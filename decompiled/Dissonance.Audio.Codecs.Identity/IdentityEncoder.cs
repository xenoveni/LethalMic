using System;

namespace Dissonance.Audio.Codecs.Identity;

internal class IdentityEncoder : IVoiceEncoder, IDisposable
{
	private readonly int _sampleRate;

	private readonly int _frameSize;

	public float PacketLoss
	{
		set
		{
		}
	}

	public int FrameSize => _frameSize;

	public int SampleRate => _sampleRate;

	public IdentityEncoder(int sampleRate, int frameSize)
	{
		_sampleRate = sampleRate;
		_frameSize = frameSize;
	}

	public ArraySegment<byte> Encode(ArraySegment<float> samples, ArraySegment<byte> array)
	{
		float[]? src = samples.Array ?? throw new ArgumentNullException("samples");
		byte[] array2 = array.Array;
		if (array2 == null)
		{
			throw new ArgumentNullException("array");
		}
		int num = samples.Count * 4;
		if (num > array.Count)
		{
			throw new ArgumentException("output buffer is too small");
		}
		Buffer.BlockCopy(src, samples.Offset, array2, array.Offset, num);
		return new ArraySegment<byte>(array.Array, array.Offset, num);
	}

	public void Reset()
	{
	}

	public void Dispose()
	{
	}
}
