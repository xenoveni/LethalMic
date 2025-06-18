using System;
using System.Collections.ObjectModel;

namespace Dissonance.Networking.Server.Admin;

public interface IServerAdmin
{
	ReadOnlyCollection<IServerClientState> Clients { get; }

	bool EnableChannelMonitoring { get; set; }

	event Action<IServerClientState> ClientJoined;

	event Action<IServerClientState> ClientLeft;

	event Action<IServerClientState, IServerClientState> VoicePacketSpoofed;
}
