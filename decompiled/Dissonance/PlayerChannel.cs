using System;
using Dissonance.Extensions;
using JetBrains.Annotations;

namespace Dissonance;

public struct PlayerChannel : IChannel<string>, IDisposable, IEquatable<PlayerChannel>
{
	private readonly ushort _subscriptionId;

	private readonly string _playerId;

	private readonly ChannelProperties _properties;

	private readonly PlayerChannels _channels;

	public ushort SubscriptionId => _subscriptionId;

	[NotNull]
	public string TargetId => _playerId;

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
			_properties.AmplitudeMultiplier = value;
		}
	}

	internal PlayerChannel(ushort subscriptionId, string playerId, PlayerChannels channels, ChannelProperties properties)
	{
		_subscriptionId = subscriptionId;
		_playerId = playerId;
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
			throw new DissonanceException("Cannot access channel properties on a closed channel.");
		}
	}

	public bool Equals(PlayerChannel other)
	{
		if (_subscriptionId == other._subscriptionId)
		{
			return string.Equals(_playerId, other._playerId);
		}
		return false;
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (obj is PlayerChannel)
		{
			return Equals((PlayerChannel)obj);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return (_subscriptionId.GetHashCode() * 397) ^ _playerId.GetFnvHashCode();
	}
}
