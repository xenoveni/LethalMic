using System;
using System.Collections.Generic;
using System.Threading;
using Dissonance.Datastructures;
using Dissonance.Networking;
using HandyCollections.Heap;
using JetBrains.Annotations;

namespace Dissonance.Audio.Playback;

internal class EncodedAudioBuffer
{
	public class VoicePacketComparer : IComparer<VoicePacket>
	{
		public int Compare(VoicePacket x, VoicePacket y)
		{
			return x.SequenceNumber.CompareTo(y.SequenceNumber);
		}
	}

	private static readonly Log Log = Logs.Create(LogCategory.Playback, typeof(EncodedAudioBuffer).Name);

	private readonly MinHeap<VoicePacket> _heap;

	private readonly Action<VoicePacket> _droppedFrameHandler;

	private volatile bool _complete;

	private int _count;

	private readonly PacketLossCalculator _loss = new PacketLossCalculator(128u);

	public int Count => _count;

	public uint SequenceNumber { get; private set; }

	public float PacketLoss => _loss.PacketLoss;

	public EncodedAudioBuffer([NotNull] Action<VoicePacket> droppedFrameHandler)
	{
		if (droppedFrameHandler == null)
		{
			throw new ArgumentNullException("droppedFrameHandler");
		}
		_droppedFrameHandler = droppedFrameHandler;
		_heap = new MinHeap<VoicePacket>(32, new VoicePacketComparer())
		{
			AllowHeapResize = true
		};
		SequenceNumber = 0u;
		_complete = false;
	}

	public void Push(VoicePacket frame)
	{
		_heap.Add(frame);
		Interlocked.Increment(ref _count);
		if (_count > 39 && _count % 10 == 0)
		{
			Log.Warn("Encoded audio heap is getting very large ({0} items)", _count);
		}
	}

	public void Stop()
	{
		_complete = true;
	}

	public bool Read(out VoicePacket? frame, out bool lostPacket)
	{
		uint sequenceNumber = SequenceNumber;
		while (_heap.Count > 0 && _heap.Minimum.SequenceNumber < sequenceNumber)
		{
			VoicePacket obj = _heap.RemoveMin();
			Interlocked.Decrement(ref _count);
			uint num = sequenceNumber - obj.SequenceNumber;
			if (num > 30)
			{
				Log.Warn("Received a very late packet ({0} packets late). This may indicate severe network congestion or a very poor frame rate. (30EF1B03-7BBC-49D3-A23E-6E84781FF29F)", num);
			}
			_droppedFrameHandler(obj);
		}
		if (_heap.Count > 0 && _heap.Minimum.SequenceNumber == sequenceNumber)
		{
			frame = _heap.RemoveMin();
			Interlocked.Decrement(ref _count);
			lostPacket = false;
		}
		else
		{
			lostPacket = true;
			if (_heap.Count > 0 && _heap.Minimum.SequenceNumber == sequenceNumber + 1)
			{
				frame = _heap.Minimum;
			}
			else
			{
				frame = null;
			}
		}
		_loss.Update(!lostPacket);
		SequenceNumber++;
		return IsComplete();
	}

	public void Reset()
	{
		while (_heap.Count > 0)
		{
			_droppedFrameHandler(_heap.RemoveMin());
			Interlocked.Decrement(ref _count);
		}
		_loss.Clear();
		_complete = false;
		SequenceNumber = 0u;
	}

	private bool IsComplete()
	{
		if (_complete)
		{
			return _heap.Count == 0;
		}
		return false;
	}
}
