using JetBrains.Annotations;

namespace Dissonance.Networking.Client;

internal struct OpenChannel
{
	private static readonly Log Log = Logs.Create(LogCategory.Network, typeof(OpenChannel).Name);

	private readonly ChannelProperties _config;

	private readonly ChannelType _type;

	private readonly ushort _recipient;

	private readonly string _name;

	private readonly bool _isClosing;

	private readonly ushort _sessionId;

	private readonly bool _sent;

	[NotNull]
	public ChannelProperties Config => _config;

	public ushort Bitfield => new ChannelBitField(_type, _sessionId, Priority, AmplitudeMultiplier, IsPositional, _isClosing).Bitfield;

	public ushort Recipient => _recipient;

	public ChannelType Type => _type;

	public bool IsClosing => _isClosing;

	public bool IsPositional => _config.Positional;

	public ChannelPriority Priority => _config.TransmitPriority;

	public float AmplitudeMultiplier => _config.AmplitudeMultiplier;

	public ushort SessionId => _sessionId;

	[NotNull]
	public string Name => _name;

	public OpenChannel(ChannelType type, ushort sessionId, ChannelProperties config, bool closing, ushort recipient, string name, bool sent = false)
	{
		_type = type;
		_sessionId = sessionId;
		_config = config;
		_isClosing = closing;
		_recipient = recipient;
		_name = name;
		_sent = sent;
	}

	[Pure]
	public OpenChannel AsClosing()
	{
		if (IsClosing)
		{
			throw Log.CreatePossibleBugException("Attempted to close a channel which is already closed", "94ED6728-F8D7-4926-9058-E23A5870BF31");
		}
		return new OpenChannel(_type, _sessionId, _config, closing: true, _recipient, _name);
	}

	[Pure]
	public OpenChannel AsOpen()
	{
		if (!IsClosing)
		{
			throw Log.CreatePossibleBugException("Attempted to open a channel which is already open", "F1880EDD-D222-4358-9C2C-4F1C72114B62");
		}
		ushort sessionId = (_sent ? ((ushort)(_sessionId + 1)) : _sessionId);
		return new OpenChannel(_type, sessionId, _config, closing: false, _recipient, _name);
	}

	[Pure]
	public OpenChannel AsSent()
	{
		return new OpenChannel(_type, _sessionId, _config, _isClosing, _recipient, _name, sent: true);
	}
}
