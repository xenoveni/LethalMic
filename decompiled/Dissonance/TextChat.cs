using System;
using Dissonance.Networking;
using JetBrains.Annotations;

namespace Dissonance;

public sealed class TextChat
{
	private readonly Func<ICommsNetwork> _getNetwork;

	public event Action<TextMessage> MessageReceived;

	internal TextChat([NotNull] Func<ICommsNetwork> getNetwork)
	{
		if (getNetwork == null)
		{
			throw new ArgumentNullException("getNetwork");
		}
		_getNetwork = getNetwork;
	}

	public void Send([NotNull] string roomName, [NotNull] string message)
	{
		if (roomName == null)
		{
			throw new ArgumentNullException("roomName", "Cannot send a text message to a null room");
		}
		if (message == null)
		{
			throw new ArgumentNullException("message", "Cannot send null text message");
		}
		_getNetwork()?.SendText(message, ChannelType.Room, roomName);
	}

	public void Whisper([NotNull] string playerName, [NotNull] string message)
	{
		if (playerName == null)
		{
			throw new ArgumentNullException("playerName", "Cannot send a text message to a null playerName");
		}
		if (message == null)
		{
			throw new ArgumentNullException("message", "Cannot send null text message");
		}
		_getNetwork()?.SendText(message, ChannelType.Player, playerName);
	}

	internal void OnMessageReceived(TextMessage obj)
	{
		this.MessageReceived?.Invoke(obj);
	}
}
