using System;
using Dissonance.Audio.Playback;
using Dissonance.Extensions;
using NAudio.Wave;

namespace Dissonance.Audio.Codecs.Silence;

internal class SilenceDecoder : IVoiceDecoder, IDisposable
{
	private readonly int _frameSize;

	private readonly WaveFormat _format;

	public WaveFormat Format => _format;

	public SilenceDecoder(FrameFormat frameFormat)
	{
		_frameSize = (int)frameFormat.FrameSize;
		_format = frameFormat.WaveFormat;
	}

	public void Dispose()
	{
	}

	public void Reset()
	{
	}

	public int Decode(EncodedBuffer input, ArraySegment<float> output)
	{
		output.Clear();
		return _frameSize;
	}
}
