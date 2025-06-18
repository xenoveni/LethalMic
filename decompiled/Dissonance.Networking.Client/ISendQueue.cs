using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Dissonance.Networking.Client;

internal interface ISendQueue<TPeer> where TPeer : struct
{
	void EnqueueReliable(ArraySegment<byte> packet);

	void EnqeueUnreliable(ArraySegment<byte> packet);

	void EnqueueReliableP2P(ushort localId, [NotNull] IList<ClientInfo<TPeer?>> destinations, ArraySegment<byte> packet);

	void EnqueueUnreliableP2P(ushort localId, [NotNull] IList<ClientInfo<TPeer?>> destinations, ArraySegment<byte> packet);

	byte[] GetSendBuffer();

	void RecycleSendBuffer(byte[] buffer);
}
