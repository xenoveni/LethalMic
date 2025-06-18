using System;

namespace Dissonance.Audio.Codecs.Opus;

internal class OpusEncoder : IVoiceEncoder, IDisposable
{
	private static readonly Log Log = Logs.Create(LogCategory.Playback, typeof(OpusEncoder).Name);

	private readonly OpusNative.OpusEncoder _encoder;

	private static readonly int[] PermittedFrameSizesSamples = new int[6] { 120, 240, 480, 960, 1920, 2880 };

	public const int FixedSampleRate = 48000;

	private readonly int _frameSize;

	public int SampleRate => 48000;

	public float PacketLoss
	{
		set
		{
			_encoder.PacketLoss = value;
		}
	}

	public int FrameSize => _frameSize;

	public OpusEncoder(AudioQuality quality, FrameSize frameSize, bool fec = true)
	{
		_encoder = new OpusNative.OpusEncoder(SampleRate, 1)
		{
			EnableForwardErrorCorrection = fec,
			Bitrate = GetTargetBitrate(quality)
		};
		_frameSize = GetFrameSize(frameSize);
	}

	private static int GetTargetBitrate(AudioQuality quality)
	{
		return quality switch
		{
			AudioQuality.Low => 10000, 
			AudioQuality.Medium => 17000, 
			AudioQuality.High => 24000, 
			_ => throw new ArgumentOutOfRangeException("quality", quality, null), 
		};
	}

	public static int GetFrameSize(FrameSize size)
	{
		return size switch
		{
			Dissonance.FrameSize.Tiny => PermittedFrameSizesSamples[2], 
			Dissonance.FrameSize.Small => PermittedFrameSizesSamples[3], 
			Dissonance.FrameSize.Medium => PermittedFrameSizesSamples[4], 
			Dissonance.FrameSize.Large => PermittedFrameSizesSamples[5], 
			_ => throw new ArgumentOutOfRangeException("size", size, null), 
		};
	}

	public ArraySegment<byte> Encode(ArraySegment<float> samples, ArraySegment<byte> encodedBuffer)
	{
		if (Array.IndexOf(PermittedFrameSizesSamples, samples.Count) == -1)
		{
			throw new ArgumentException(Log.PossibleBugMessage($"Incorrect frame size '{samples.Count}'", "6AFD9ADF-1D15-4197-99E9-5A19ECB8CD20"), "samples");
		}
		int count = _encoder.EncodeFloats(samples, encodedBuffer);
		return new ArraySegment<byte>(encodedBuffer.Array, encodedBuffer.Offset, count);
	}

	public void Reset()
	{
		_encoder.Reset();
	}

	public void Dispose()
	{
		_encoder.Dispose();
	}
}
