using System;
using Dissonance.Extensions;
using JetBrains.Annotations;
using UnityEngine;

namespace Dissonance;

public struct RoomChannel : IChannel<string>, IDisposable, IEquatable<RoomChannel>
{
	private static readonly Log Log = Logs.Create(LogCategory.Core, typeof(RoomChannel).Name);

	private readonly ushort _subscriptionId;

	private readonly string _roomId;

	private readonly ChannelProperties _properties;

	private readonly RoomChannels _channels;

	public ushort SubscriptionId => _subscriptionId;

	[NotNull]
	public string TargetId => _roomId;

	ChannelProperties IChannel<string>.Properties => _properties;

	[NotNull]
	internal ChannelProperties Properties => _properties;

	public bool IsOpen => _channels.Contains(this);

	public bool Positional
	{
		get
		{
			CheckValidProperties();
			return _properties.Positional;
		}
		set
		{
			CheckValidProperties();
			_properties.Positional = value;
		}
	}

	public ChannelPriority Priority
	{
		get
		{
			CheckValidProperties();
			return _properties.Priority;
		}
		set
		{
			CheckValidProperties();
			_properties.Priority = value;
		}
	}

	public float Volume
	{
		get
		{
			CheckValidProperties();
			return _properties.AmplitudeMultiplier;
		}
		set
		{
			CheckValidProperties();
			_properties.AmplitudeMultiplier = Mathf.Clamp(value, 0f, 2f);
		}
	}

	internal RoomChannel(ushort subscriptionId, string roomId, RoomChannels channels, ChannelProperties properties)
	{
		_subscriptionId = subscriptionId;
		_roomId = roomId;
		_channels = channels;
		_properties = properties;
	}

	public void Dispose()
	{
		_channels.Close(this);
	}

	private void CheckValidProperties()
	{
		if (_properties.Id != _subscriptionId)
		{
			throw Log.CreateUserErrorException("Attempted to access a disposed channel", "Attempting to get or set channel properties after calling Dispose() on a channel", "https://placeholder-software.co.uk/dissonance/docs/Tutorials/Directly-Using-Channels", "DE77DE73-8DBF-4802-A413-B9A5D77A5189");
		}
	}

	public bool Equals(RoomChannel other)
	{
		if (_subscriptionId == other._subscriptionId)
		{
			return string.Equals(_roomId, other._roomId);
		}
		return false;
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (obj is RoomChannel)
		{
			return Equals((RoomChannel)obj);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return (_subscriptionId.GetHashCode() * 397) ^ _roomId.GetFnvHashCode();
	}
}
