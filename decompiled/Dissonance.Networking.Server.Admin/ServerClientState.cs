using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace Dissonance.Networking.Server.Admin;

internal class ServerClientState<TServer, TClient, TPeer> : IServerClientState where TServer : BaseServer<TServer, TClient, TPeer> where TClient : BaseClient<TServer, TClient, TPeer> where TPeer : struct, IEquatable<TPeer>
{
	private static readonly Log Log = new Log(2, typeof(ServerClientState<TServer, TClient, TPeer>).Name);

	private readonly TServer _server;

	private readonly ClientInfo<TPeer> _peer;

	private readonly List<string> _rooms;

	private readonly ReadOnlyCollection<string> _roomsReadonly;

	private readonly List<RemoteChannel> _channels;

	private readonly ReadOnlyCollection<RemoteChannel> _channelsReadonly;

	public ClientInfo<TPeer> Peer => _peer;

	public string Name => _peer.PlayerName;

	public bool IsConnected => _peer.IsConnected;

	public ReadOnlyCollection<string> Rooms => _roomsReadonly;

	public DateTime LastChannelUpdateUtc { get; private set; }

	public ReadOnlyCollection<RemoteChannel> Channels => _channelsReadonly;

	public event Action<IServerClientState, string> OnStartedListeningToRoom;

	public event Action<IServerClientState, string> OnStoppedListeningToRoom;

	public ServerClientState(TServer server, ClientInfo<TPeer> peer)
	{
		_server = server;
		_peer = peer;
		_rooms = new List<string>();
		_roomsReadonly = new ReadOnlyCollection<string>(_rooms);
		_channels = new List<RemoteChannel>();
		_channelsReadonly = new ReadOnlyCollection<RemoteChannel>(_channels);
	}

	public void RemoveFromRoom([NotNull] string roomName)
	{
		if (roomName == null)
		{
			throw new ArgumentNullException("roomName");
		}
		PacketWriter packetWriter = new PacketWriter(new byte[10 + roomName.Length * 4]);
		packetWriter.WriteDeltaChannelState(_server.SessionId, joined: false, _peer.PlayerId, roomName);
		_server.NetworkReceivedPacket(_peer.Connection, packetWriter.Written);
	}

	public void Reset()
	{
		PacketWriter packetWriter = new PacketWriter(new byte[7]);
		packetWriter.WriteErrorWrongSession(_server.SessionId + 1);
		_server.SendUnreliable(new List<TPeer> { _peer.Connection }, packetWriter.Written);
	}

	public void InvokeOnEnteredRoom(string name)
	{
		if (!_rooms.Contains(name))
		{
			_rooms.Add(name);
		}
		Action<IServerClientState, string> action = this.OnStartedListeningToRoom;
		if (action != null)
		{
			try
			{
				action(this, name);
			}
			catch (Exception p)
			{
				Log.Error("Exception encountered invoking `PlayerJoined` event handler: {0}", p);
			}
		}
	}

	public void InvokeOnExitedRoom(string name)
	{
		_rooms.Remove(name);
		Action<IServerClientState, string> action = this.OnStoppedListeningToRoom;
		if (action != null)
		{
			try
			{
				action(this, name);
			}
			catch (Exception p)
			{
				Log.Error("Exception encountered invoking `PlayerJoined` event handler: {0}", p);
			}
		}
	}

	public void UpdateChannels([NotNull] List<RemoteChannel> channels)
	{
		_channels.Clear();
		_channels.AddRange(channels);
		LastChannelUpdateUtc = DateTime.UtcNow;
	}
}
