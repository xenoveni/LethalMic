namespace Dissonance.Networking.Client;

internal struct VoicePacketOptions
{
	private const byte EXTENDED_RANGE_FLAG = 128;

	private readonly byte _bitfield;

	public int ChannelSessionRange
	{
		get
		{
			if (!IsChannelSessionExtendedRange)
			{
				return 4;
			}
			return 128;
		}
	}

	public bool IsChannelSessionExtendedRange => (_bitfield & 0x80) != 0;

	public byte ChannelSession => (byte)(_bitfield & 0x7F);

	public byte Bitfield => _bitfield;

	private VoicePacketOptions(byte bitfield)
	{
		_bitfield = bitfield;
	}

	public static VoicePacketOptions Unpack(byte bitfield)
	{
		return new VoicePacketOptions(bitfield);
	}

	public static VoicePacketOptions Pack(byte channelSession)
	{
		return new VoicePacketOptions((byte)(0x80 | (channelSession % 128)));
	}
}
