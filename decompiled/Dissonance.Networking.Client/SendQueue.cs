using System;
using System.Collections.Generic;
using Dissonance.Datastructures;
using Dissonance.Threading;
using JetBrains.Annotations;

namespace Dissonance.Networking.Client;

internal class SendQueue<TPeer> : ISendQueue<TPeer> where TPeer : struct
{
	private static readonly Log Log = Logs.Create(LogCategory.Network, typeof(SendQueue<TPeer>).Name);

	private readonly IClient<TPeer> _client;

	private readonly ReadonlyLockedValue<List<ArraySegment<byte>>> _serverReliableQueue = new ReadonlyLockedValue<List<ArraySegment<byte>>>(new List<ArraySegment<byte>>());

	private readonly ReadonlyLockedValue<List<ArraySegment<byte>>> _serverUnreliableQueue = new ReadonlyLockedValue<List<ArraySegment<byte>>>(new List<ArraySegment<byte>>());

	private readonly ReadonlyLockedValue<List<KeyValuePair<List<ClientInfo<TPeer?>>, ArraySegment<byte>>>> _reliableP2PQueue = new ReadonlyLockedValue<List<KeyValuePair<List<ClientInfo<TPeer?>>, ArraySegment<byte>>>>(new List<KeyValuePair<List<ClientInfo<TPeer?>>, ArraySegment<byte>>>());

	private readonly ReadonlyLockedValue<List<KeyValuePair<List<ClientInfo<TPeer?>>, ArraySegment<byte>>>> _unreliableP2PQueue = new ReadonlyLockedValue<List<KeyValuePair<List<ClientInfo<TPeer?>>, ArraySegment<byte>>>>(new List<KeyValuePair<List<ClientInfo<TPeer?>>, ArraySegment<byte>>>());

	private readonly ReadonlyLockedValue<Pool<byte[]>> _sendBufferPool;

	private readonly ConcurrentPool<List<ClientInfo<TPeer?>>> _listPool = new ConcurrentPool<List<ClientInfo<TPeer?>>>(32, () => new List<ClientInfo<TPeer?>>());

	private readonly List<byte[]> _tmpRecycleQueue = new List<byte[]>();

	public SendQueue([NotNull] IClient<TPeer> client, [NotNull] ReadonlyLockedValue<Pool<byte[]>> bytePool)
	{
		if (client == null)
		{
			throw new ArgumentNullException("client");
		}
		if (bytePool == null)
		{
			throw new ArgumentNullException("bytePool");
		}
		_client = client;
		_sendBufferPool = bytePool;
	}

	public void Update()
	{
		using (ReadonlyLockedValue<List<ArraySegment<byte>>>.Unlocker unlocker = _serverReliableQueue.Lock())
		{
			List<ArraySegment<byte>> value = unlocker.Value;
			for (int i = 0; i < value.Count; i++)
			{
				ArraySegment<byte> arraySegment = value[i];
				_client.SendReliable(arraySegment);
				_tmpRecycleQueue.Add(arraySegment.Array);
			}
			value.Clear();
		}
		using (ReadonlyLockedValue<List<ArraySegment<byte>>>.Unlocker unlocker2 = _serverUnreliableQueue.Lock())
		{
			List<ArraySegment<byte>> value2 = unlocker2.Value;
			for (int j = 0; j < value2.Count; j++)
			{
				ArraySegment<byte> arraySegment2 = value2[j];
				_client.SendUnreliable(arraySegment2);
				_tmpRecycleQueue.Add(arraySegment2.Array);
			}
			value2.Clear();
		}
		using (ReadonlyLockedValue<List<KeyValuePair<List<ClientInfo<TPeer?>>, ArraySegment<byte>>>>.Unlocker unlocker3 = _reliableP2PQueue.Lock())
		{
			List<KeyValuePair<List<ClientInfo<TPeer?>>, ArraySegment<byte>>> value3 = unlocker3.Value;
			for (int k = 0; k < value3.Count; k++)
			{
				KeyValuePair<List<ClientInfo<TPeer?>>, ArraySegment<byte>> keyValuePair = value3[k];
				_client.SendReliableP2P(keyValuePair.Key, keyValuePair.Value);
				_tmpRecycleQueue.Add(keyValuePair.Value.Array);
				keyValuePair.Key.Clear();
				_listPool.Put(keyValuePair.Key);
			}
			value3.Clear();
		}
		using (ReadonlyLockedValue<List<KeyValuePair<List<ClientInfo<TPeer?>>, ArraySegment<byte>>>>.Unlocker unlocker4 = _unreliableP2PQueue.Lock())
		{
			List<KeyValuePair<List<ClientInfo<TPeer?>>, ArraySegment<byte>>> value4 = unlocker4.Value;
			for (int l = 0; l < value4.Count; l++)
			{
				KeyValuePair<List<ClientInfo<TPeer?>>, ArraySegment<byte>> keyValuePair2 = value4[l];
				_client.SendUnreliableP2P(keyValuePair2.Key, keyValuePair2.Value);
				_tmpRecycleQueue.Add(keyValuePair2.Value.Array);
				keyValuePair2.Key.Clear();
				_listPool.Put(keyValuePair2.Key);
			}
			value4.Clear();
		}
		using (ReadonlyLockedValue<Pool<byte[]>>.Unlocker unlocker5 = _sendBufferPool.Lock())
		{
			for (int m = 0; m < _tmpRecycleQueue.Count; m++)
			{
				byte[] array = _tmpRecycleQueue[m];
				if (array != null)
				{
					unlocker5.Value.Put(array);
				}
			}
		}
		_tmpRecycleQueue.Clear();
	}

	private static int Drop<T>([NotNull] ReadonlyLockedValue<List<T>> l)
	{
		using ReadonlyLockedValue<List<T>>.Unlocker unlocker = l.Lock();
		int count = unlocker.Value.Count;
		unlocker.Value.Clear();
		return count;
	}

	public void Stop()
	{
		Drop(_serverReliableQueue);
		Drop(_serverUnreliableQueue);
		Drop(_reliableP2PQueue);
		Drop(_unreliableP2PQueue);
	}

	public void EnqueueReliable(ArraySegment<byte> packet)
	{
		if (packet.Array == null)
		{
			throw new ArgumentNullException("packet");
		}
		using ReadonlyLockedValue<List<ArraySegment<byte>>>.Unlocker unlocker = _serverReliableQueue.Lock();
		unlocker.Value.Add(packet);
	}

	public void EnqeueUnreliable(ArraySegment<byte> packet)
	{
		if (packet.Array == null)
		{
			throw new ArgumentNullException("packet");
		}
		using ReadonlyLockedValue<List<ArraySegment<byte>>>.Unlocker unlocker = _serverUnreliableQueue.Lock();
		unlocker.Value.Add(packet);
	}

	public void EnqueueReliableP2P(ushort localId, IList<ClientInfo<TPeer?>> destinations, ArraySegment<byte> packet)
	{
		if (destinations == null)
		{
			throw new ArgumentNullException("destinations");
		}
		if (packet.Array == null)
		{
			throw new ArgumentNullException("packet");
		}
		using ReadonlyLockedValue<List<KeyValuePair<List<ClientInfo<TPeer?>>, ArraySegment<byte>>>>.Unlocker unlocker = _reliableP2PQueue.Lock();
		EnqueueP2P(localId, destinations, unlocker.Value, packet);
	}

	public void EnqueueUnreliableP2P(ushort localId, IList<ClientInfo<TPeer?>> destinations, ArraySegment<byte> packet)
	{
		if (destinations == null)
		{
			throw new ArgumentNullException("destinations");
		}
		if (packet.Array == null)
		{
			throw new ArgumentNullException("packet");
		}
		using ReadonlyLockedValue<List<KeyValuePair<List<ClientInfo<TPeer?>>, ArraySegment<byte>>>>.Unlocker unlocker = _unreliableP2PQueue.Lock();
		EnqueueP2P(localId, destinations, unlocker.Value, packet);
	}

	public byte[] GetSendBuffer()
	{
		using ReadonlyLockedValue<Pool<byte[]>>.Unlocker unlocker = _sendBufferPool.Lock();
		return unlocker.Value.Get();
	}

	public void RecycleSendBuffer([NotNull] byte[] buffer)
	{
		if (buffer == null)
		{
			throw new ArgumentNullException("buffer");
		}
		using ReadonlyLockedValue<Pool<byte[]>>.Unlocker unlocker = _sendBufferPool.Lock();
		unlocker.Value.Put(buffer);
	}

	private void EnqueueP2P(ushort localId, [NotNull] ICollection<ClientInfo<TPeer?>> destinations, [NotNull] ICollection<KeyValuePair<List<ClientInfo<TPeer?>>, ArraySegment<byte>>> queue, ArraySegment<byte> packet)
	{
		if (packet.Array == null)
		{
			throw new ArgumentNullException("packet");
		}
		if (destinations == null)
		{
			throw new ArgumentNullException("destinations");
		}
		if (queue == null)
		{
			throw new ArgumentNullException("queue");
		}
		if (destinations.Count == 0)
		{
			return;
		}
		List<ClientInfo<TPeer?>> list = _listPool.Get();
		list.Clear();
		list.AddRange(destinations);
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].PlayerId == localId)
			{
				list.RemoveAt(i);
				break;
			}
		}
		if (list.Count == 0)
		{
			_listPool.Put(list);
		}
		else
		{
			queue.Add(new KeyValuePair<List<ClientInfo<TPeer?>>, ArraySegment<byte>>(list, packet));
		}
	}
}
