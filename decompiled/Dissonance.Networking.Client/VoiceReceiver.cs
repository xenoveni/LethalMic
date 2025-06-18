using System;
using System.Collections.Generic;
using Dissonance.Datastructures;
using JetBrains.Annotations;

namespace Dissonance.Networking.Client;

internal class VoiceReceiver<TPeer> where TPeer : struct
{
	private static readonly Log Log = Logs.Create(LogCategory.Network, typeof(VoiceReceiver<TPeer>).Name);

	private static readonly TimeSpan ActiveTimeout = TimeSpan.FromSeconds(1.5);

	private static readonly TimeSpan InactiveTimeout = TimeSpan.FromSeconds(15.0);

	private readonly ISession _session;

	private readonly IClientCollection<TPeer?> _clients;

	private readonly EventQueue _events;

	private readonly Rooms _rooms;

	private readonly ConcurrentPool<List<RemoteChannel>> _channelListPool;

	private readonly List<PeerVoiceReceiver> _receivers = new List<PeerVoiceReceiver>();

	public VoiceReceiver(ISession session, IClientCollection<TPeer?> clients, EventQueue events, Rooms rooms, ConcurrentPool<List<RemoteChannel>> channelListPool)
	{
		_session = session;
		_clients = clients;
		_events = events;
		_rooms = rooms;
		_channelListPool = channelListPool;
		_events.OnEnqueuePlayerLeft += OnPlayerLeft;
	}

	private void OnPlayerLeft([NotNull] string name)
	{
		for (int i = 0; i < _receivers.Count; i++)
		{
			PeerVoiceReceiver peerVoiceReceiver = _receivers[i];
			if (peerVoiceReceiver.Name == name)
			{
				if (peerVoiceReceiver.Open)
				{
					peerVoiceReceiver.StopSpeaking();
				}
				_receivers.RemoveAt(i);
				break;
			}
		}
	}

	public void Stop()
	{
		for (int i = 0; i < _receivers.Count; i++)
		{
			if (_receivers[i] != null && _receivers[i].Open)
			{
				_receivers[i].StopSpeaking();
			}
		}
		_receivers.Clear();
	}

	public void Update(DateTime utcNow)
	{
		CheckTimeouts(utcNow);
	}

	private void CheckTimeouts(DateTime utcNow)
	{
		for (int num = _receivers.Count - 1; num >= 0; num--)
		{
			_receivers[num]?.CheckTimeout(utcNow, ActiveTimeout, InactiveTimeout);
		}
	}

	public void ReceiveVoiceData(ref PacketReader reader, DateTime? utcNow = null)
	{
		if (!_session.LocalId.HasValue)
		{
			return;
		}
		reader.ReadVoicePacketHeader1(out var senderId);
		if (_clients.TryGetClientInfoById(senderId, out var info))
		{
			if (info.VoiceReceiver == null)
			{
				info.VoiceReceiver = new PeerVoiceReceiver(info.PlayerName, _session.LocalId.Value, _session.LocalName, _events, _rooms, _channelListPool);
				_receivers.Add(info.VoiceReceiver);
			}
			info.VoiceReceiver.ReceivePacket(ref reader, utcNow ?? DateTime.UtcNow);
		}
	}
}
