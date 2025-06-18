namespace Dissonance.Networking;

internal enum MessageTypes : byte
{
	ClientState = 1,
	VoiceData,
	TextData,
	HandshakeRequest,
	HandshakeResponse,
	ErrorWrongSession,
	ServerRelayReliable,
	ServerRelayUnreliable,
	DeltaChannelState,
	RemoveClient,
	HandshakeP2P
}
