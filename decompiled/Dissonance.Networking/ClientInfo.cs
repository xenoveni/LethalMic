using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dissonance.Extensions;
using Dissonance.Networking.Client;
using JetBrains.Annotations;

namespace Dissonance.Networking;

internal struct ClientInfo
{
	public string PlayerName { get; private set; }

	public ushort PlayerId { get; private set; }

	public CodecSettings CodecSettings { get; private set; }

	public ClientInfo(string playerName, ushort playerId, CodecSettings codecSettings)
	{
		this = default(ClientInfo);
		PlayerName = playerName;
		PlayerId = playerId;
		CodecSettings = codecSettings;
	}
}
public class ClientInfo<TPeer> : IEquatable<ClientInfo<TPeer>>
{
	private static readonly Log Log = Logs.Create(LogCategory.Network, typeof(ClientInfo<TPeer>).Name);

	private readonly string _playerName;

	private readonly ushort _playerId;

	private readonly CodecSettings _codecSettings;

	private readonly List<string> _rooms = new List<string>();

	private readonly ReadOnlyCollection<string> _roomsReadonly;

	[NotNull]
	public string PlayerName => _playerName;

	public ushort PlayerId => _playerId;

	public CodecSettings CodecSettings => _codecSettings;

	[NotNull]
	internal ReadOnlyCollection<string> Rooms => _roomsReadonly;

	[CanBeNull]
	public TPeer Connection { get; internal set; }

	public bool IsConnected { get; internal set; }

	internal PeerVoiceReceiver VoiceReceiver { get; set; }

	public ClientInfo(string playerName, ushort playerId, CodecSettings codecSettings, [CanBeNull] TPeer connection)
	{
		_roomsReadonly = new ReadOnlyCollection<string>(_rooms);
		_playerName = playerName;
		_playerId = playerId;
		_codecSettings = codecSettings;
		Connection = connection;
		IsConnected = true;
	}

	public override string ToString()
	{
		return $"Client '{PlayerName}/{PlayerId}/{Connection}'";
	}

	public bool Equals(ClientInfo<TPeer> other)
	{
		if (other == null)
		{
			return false;
		}
		if (this == other)
		{
			return true;
		}
		if (string.Equals(_playerName, other._playerName))
		{
			return _playerId == other._playerId;
		}
		return false;
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (this == obj)
		{
			return true;
		}
		if (obj.GetType() != GetType())
		{
			return false;
		}
		return Equals((ClientInfo<TPeer>)obj);
	}

	public override int GetHashCode()
	{
		return (_playerName.GetFnvHashCode() * 397) ^ _playerId.GetHashCode();
	}

	public bool AddRoom([NotNull] string roomName)
	{
		if (roomName == null)
		{
			throw new ArgumentNullException("roomName");
		}
		int num = _rooms.BinarySearch(roomName);
		if (num < 0)
		{
			_rooms.Insert(~num, roomName);
			return true;
		}
		return false;
	}

	public bool RemoveRoom([NotNull] string roomName)
	{
		if (roomName == null)
		{
			throw new ArgumentNullException("roomName");
		}
		int num = _rooms.BinarySearch(roomName);
		if (num >= 0)
		{
			_rooms.RemoveAt(num);
			return true;
		}
		return false;
	}
}
