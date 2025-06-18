using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dissonance.Audio.Playback;
using JetBrains.Annotations;

namespace Dissonance.Networking.Server.Admin;

internal class ServerAdmin<TServer, TClient, TPeer> : IServerAdmin where TServer : BaseServer<TServer, TClient, TPeer> where TClient : BaseClient<TServer, TClient, TPeer> where TPeer : struct, IEquatable<TPeer>
{
	private static readonly Log Log = new Log(2, typeof(ServerAdmin<TServer, TClient, TPeer>).Name);

	private readonly TServer _server;

	private readonly Dictionary<ushort, string> _knownRoomNames = new Dictionary<ushort, string>();

	private readonly List<IServerClientState> _clients;

	private readonly ReadOnlyCollection<IServerClientState> _readonlyClients;

	private readonly List<RemoteChannel> _channelsTmp = new List<RemoteChannel>();

	public ReadOnlyCollection<IServerClientState> Clients => _readonlyClients;

	public bool EnableChannelMonitoring { get; set; }

	public event Action<IServerClientState> ClientJoined;

	public event Action<IServerClientState> ClientLeft;

	public event Action<IServerClientState, IServerClientState> VoicePacketSpoofed;

	public ServerAdmin(TServer server)
	{
		_server = server;
		_clients = new List<IServerClientState>();
		_readonlyClients = new ReadOnlyCollection<IServerClientState>(_clients);
	}

	[CanBeNull]
	private ServerClientState<TServer, TClient, TPeer> FindPlayer([NotNull] ClientInfo<TPeer> peer)
	{
		for (int i = 0; i < _clients.Count; i++)
		{
			ServerClientState<TServer, TClient, TPeer> serverClientState = (ServerClientState<TServer, TClient, TPeer>)_clients[i];
			if (serverClientState.Peer.Equals(peer))
			{
				return serverClientState;
			}
		}
		return null;
	}

	[CanBeNull]
	private ServerClientState<TServer, TClient, TPeer> FindPlayer(ushort id)
	{
		for (int i = 0; i < _clients.Count; i++)
		{
			ServerClientState<TServer, TClient, TPeer> serverClientState = (ServerClientState<TServer, TClient, TPeer>)_clients[i];
			if (serverClientState.Peer.PlayerId.Equals(id))
			{
				return serverClientState;
			}
		}
		return null;
	}

	[CanBeNull]
	private ServerClientState<TServer, TClient, TPeer> FindPlayer(TPeer peer)
	{
		for (int i = 0; i < _clients.Count; i++)
		{
			ServerClientState<TServer, TClient, TPeer> serverClientState = (ServerClientState<TServer, TClient, TPeer>)_clients[i];
			if (serverClientState.Peer.Connection.Equals(peer))
			{
				return serverClientState;
			}
		}
		return null;
	}

	public void InvokeOnClientEnteredRoom([NotNull] ClientInfo<TPeer> peer, string name)
	{
		ServerClientState<TServer, TClient, TPeer> serverClientState = FindPlayer(peer);
		if (serverClientState == null)
		{
			Log.Error("Failed to find player to add to room: {0}", peer.PlayerName);
			return;
		}
		serverClientState.InvokeOnEnteredRoom(name);
		_knownRoomNames[name.ToRoomId()] = name;
	}

	public void InvokeOnClientExitedRoom([NotNull] ClientInfo<TPeer> peer, string name)
	{
		ServerClientState<TServer, TClient, TPeer> serverClientState = FindPlayer(peer);
		if (serverClientState == null)
		{
			Log.Error("Failed to find player to remove from room: {0}", peer.PlayerName);
		}
		else
		{
			serverClientState.InvokeOnExitedRoom(name);
		}
	}

	public void InvokeOnClientJoined([NotNull] ClientInfo<TPeer> peer)
	{
		ServerClientState<TServer, TClient, TPeer> serverClientState = new ServerClientState<TServer, TClient, TPeer>(_server, peer);
		_clients.Add(serverClientState);
		Action<IServerClientState> action = this.ClientJoined;
		if (action != null)
		{
			try
			{
				action(serverClientState);
			}
			catch (Exception p)
			{
				Log.Error("Exception encountered invoking `PlayerJoined` event handler: {0}", p);
			}
		}
	}

	public void InvokeOnClientLeft([NotNull] ClientInfo<TPeer> peer)
	{
		ServerClientState<TServer, TClient, TPeer> serverClientState = FindPlayer(peer);
		if (serverClientState == null)
		{
			Log.Error("Failed to find player to remove: {0}", peer.PlayerName);
			return;
		}
		_clients.Remove(serverClientState);
		Action<IServerClientState> action = this.ClientLeft;
		if (action == null)
		{
			return;
		}
		try
		{
			action(serverClientState);
		}
		catch (Exception p)
		{
			Log.Error("Exception encountered invoking `PlayerLeft` event handler: {0}", p);
		}
	}

	public void InvokeOnRelayingPacket(ArraySegment<byte> payload, TPeer source)
	{
		if (!EnableChannelMonitoring)
		{
			return;
		}
		PacketReader packetReader = new PacketReader(payload);
		if (!packetReader.ReadPacketHeader(out var messageType))
		{
			Log.Error("Ignoring relayed packet - magic number is incorrect");
		}
		else
		{
			if (messageType != MessageTypes.VoiceData || packetReader.ReadUInt32() != _server.SessionId)
			{
				return;
			}
			ServerClientState<TServer, TClient, TPeer> serverClientState = FindPlayer(source);
			if (serverClientState == null)
			{
				return;
			}
			packetReader.ReadVoicePacketHeader1(out var senderId);
			if (senderId != serverClientState.Peer.PlayerId)
			{
				ServerClientState<TServer, TClient, TPeer> spoofee = FindPlayer(senderId);
				InvokeOnVoicePacketSpoof(serverClientState, spoofee);
				return;
			}
			packetReader.ReadVoicePacketHeader2(out var _, out var _, out var numChannels);
			_channelsTmp.Clear();
			for (int i = 0; i < numChannels; i++)
			{
				packetReader.ReadVoicePacketChannel(out var bitfield, out var recipient);
				if (bitfield.IsClosing)
				{
					continue;
				}
				string value;
				if (bitfield.Type == ChannelType.Player)
				{
					ServerClientState<TServer, TClient, TPeer> serverClientState2 = FindPlayer(recipient);
					if (serverClientState2 == null)
					{
						continue;
					}
					value = serverClientState2.Name;
				}
				else if (bitfield.Type != ChannelType.Room || !_knownRoomNames.TryGetValue(recipient, out value))
				{
					continue;
				}
				_channelsTmp.Add(new RemoteChannel(value, bitfield.Type, new PlaybackOptions(bitfield.IsPositional, bitfield.AmplitudeMultiplier, bitfield.Priority)));
			}
			serverClientState.UpdateChannels(_channelsTmp);
		}
	}

	private void InvokeOnVoicePacketSpoof([NotNull] IServerClientState spoofer, [CanBeNull] IServerClientState spoofee)
	{
		this.VoicePacketSpoofed?.Invoke(spoofer, spoofee);
	}
}
