using System;
using NAudio.Wave;

namespace Dissonance.Audio.Codecs.Identity;

internal class IdentityDecoder : IVoiceDecoder, IDisposable
{
	private readonly WaveFormat _format;

	public WaveFormat Format => _format;

	public IdentityDecoder(WaveFormat format)
	{
		_format = format;
	}

	public void Reset()
	{
	}

	public int Decode(EncodedBuffer input, ArraySegment<float> output)
	{
		if (!input.Encoded.HasValue || input.PacketLost)
		{
			Array.Clear(output.Array, output.Offset, output.Count);
			return output.Count;
		}
		byte[]? src = input.Encoded.Value.Array ?? throw new ArgumentNullException("input");
		float[] array = output.Array;
		if (array == null)
		{
			throw new ArgumentNullException("output");
		}
		int count = input.Encoded.Value.Count;
		if (count > output.Count * 4)
		{
			throw new ArgumentException("output buffer is too small");
		}
		Buffer.BlockCopy(src, input.Encoded.Value.Offset, array, output.Offset, count);
		return input.Encoded.Value.Count / 4;
	}

	public void Dispose()
	{
	}
}
