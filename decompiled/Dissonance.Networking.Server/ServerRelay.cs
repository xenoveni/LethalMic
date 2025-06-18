using System;
using System.Collections.Generic;
using Dissonance.Extensions;

namespace Dissonance.Networking.Server;

internal class ServerRelay<TPeer>
{
	private static readonly Log Log = Logs.Create(LogCategory.Network, typeof(ServerRelay<TPeer>).Name);

	private readonly IServer<TPeer> _server;

	private readonly BaseClientCollection<TPeer> _peers;

	private readonly List<TPeer> _tmpPeerBuffer = new List<TPeer>();

	private readonly List<ushort> _tmpIdBuffer = new List<ushort>();

	public event Action<ArraySegment<byte>, TPeer> OnRelayingPacket;

	public ServerRelay(IServer<TPeer> server, BaseClientCollection<TPeer> peers)
	{
		_server = server;
		_peers = peers;
	}

	public void ProcessPacketRelay(ref PacketReader reader, bool reliable, TPeer source)
	{
		_tmpIdBuffer.Clear();
		reader.ReadRelay(_tmpIdBuffer, out var data);
		if (!new PacketReader(data).ReadPacketHeader(out var messageType))
		{
			Log.Error("Dropping relayed packet - magic number is incorrect");
		}
		else
		{
			if (messageType == MessageTypes.HandshakeP2P)
			{
				return;
			}
			_tmpPeerBuffer.Clear();
			for (int i = 0; i < _tmpIdBuffer.Count; i++)
			{
				if (!_peers.TryGetClientInfoById(_tmpIdBuffer[i], out var info))
				{
					Log.Warn("Attempted to relay packet to unknown/disconnected peer ({0})", _tmpIdBuffer[i]);
				}
				else
				{
					_tmpPeerBuffer.Add(info.Connection);
				}
			}
			data = data.CopyToSegment(data.Array);
			this.OnRelayingPacket?.Invoke(data, source);
			if (reliable)
			{
				_server.SendReliable(_tmpPeerBuffer, data);
			}
			else
			{
				_server.SendUnreliable(_tmpPeerBuffer, data);
			}
			_tmpIdBuffer.Clear();
			_tmpPeerBuffer.Clear();
		}
	}
}
