using Dissonance.Audio.Codecs;

namespace Dissonance;

public struct CodecSettings
{
	private readonly Codec _codec;

	private readonly uint _frameSize;

	private readonly int _sampleRate;

	public Codec Codec => _codec;

	public uint FrameSize => _frameSize;

	public int SampleRate => _sampleRate;

	public CodecSettings(Codec codec, uint frameSize, int sampleRate)
	{
		_codec = codec;
		_frameSize = frameSize;
		_sampleRate = sampleRate;
	}

	public override string ToString()
	{
		return $"Codec: {Codec}, FrameSize: {FrameSize}, SampleRate: {(float)SampleRate / 1000f:##.##}kHz";
	}
}
