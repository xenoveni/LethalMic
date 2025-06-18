using System;

namespace Dissonance.Networking;

internal struct ChannelBitField
{
	private const ushort TypeMask = 1;

	private const ushort PositionalMask = 2;

	private const ushort ClosureMask = 4;

	private const ushort PriorityOffset = 3;

	private const ushort PriorityMask = 24;

	private const ushort SessionIdOffset = 5;

	private const ushort SessionIdMask = 97;

	private const ushort AmplitudeOffset = 8;

	private const ushort AmplitudeMask = 65280;

	private readonly ushort _bitfield;

	public ushort Bitfield => _bitfield;

	public ChannelType Type
	{
		get
		{
			if ((_bitfield & 1) == 1)
			{
				return ChannelType.Room;
			}
			return ChannelType.Player;
		}
	}

	public bool IsClosing => (_bitfield & 4) == 4;

	public bool IsPositional => (_bitfield & 2) == 2;

	public ChannelPriority Priority => ((_bitfield & 0x18) >> 3) switch
	{
		1 => ChannelPriority.Low, 
		2 => ChannelPriority.Medium, 
		3 => ChannelPriority.High, 
		_ => ChannelPriority.Default, 
	};

	public float AmplitudeMultiplier => (float)((_bitfield & 0xFF00) >> 8) / 255f * 2f;

	public int SessionId => (_bitfield & 0x61) >> 5;

	public ChannelBitField(ushort bitfield)
	{
		_bitfield = bitfield;
	}

	public ChannelBitField(ChannelType type, int sessionId, ChannelPriority priority, float amplitudeMult, bool positional, bool closing)
	{
		this = default(ChannelBitField);
		_bitfield = 0;
		if (type == ChannelType.Room)
		{
			_bitfield |= 1;
		}
		if (positional)
		{
			_bitfield |= 2;
		}
		if (closing)
		{
			_bitfield |= 4;
		}
		_bitfield |= PackPriority(priority);
		_bitfield |= (ushort)(sessionId % 4 << 5);
		byte b = (byte)Math.Round(Math.Min(2f, Math.Max(0f, amplitudeMult)) / 2f * 255f);
		_bitfield |= (ushort)(b << 8);
	}

	private static ushort PackPriority(ChannelPriority priority)
	{
		return priority switch
		{
			ChannelPriority.Low => 8, 
			ChannelPriority.Medium => 16, 
			ChannelPriority.High => 24, 
			_ => 0, 
		};
	}
}
