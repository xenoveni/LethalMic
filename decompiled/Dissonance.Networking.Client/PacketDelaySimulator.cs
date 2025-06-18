using System;

namespace Dissonance.Networking.Client;

internal class PacketDelaySimulator
{
	private readonly Random _rnd = new Random();

	private static bool IsOrderedReliable(MessageTypes header)
	{
		return header != MessageTypes.VoiceData;
	}

	public bool ShouldLose(ArraySegment<byte> packet)
	{
		return false;
	}
}
