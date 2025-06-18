using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Dissonance.Networking;

internal class TrafficCounter
{
	private uint _runningTotal;

	private readonly Queue<KeyValuePair<DateTime, uint>> _updated = new Queue<KeyValuePair<DateTime, uint>>(64);

	public uint Packets { get; private set; }

	public uint Bytes { get; private set; }

	public uint BytesPerSecond { get; private set; }

	public void Update(int bytes, DateTime? now = null)
	{
		if (bytes < 0)
		{
			throw new ArgumentOutOfRangeException("bytes");
		}
		Packets++;
		Bytes += (uint)bytes;
		DateTime dateTime = now ?? DateTime.UtcNow;
		_updated.Enqueue(new KeyValuePair<DateTime, uint>(dateTime, (uint)bytes));
		_runningTotal += (uint)bytes;
		if (dateTime - _updated.Peek().Key >= TimeSpan.FromSeconds(10.0))
		{
			_runningTotal -= _updated.Dequeue().Value;
			BytesPerSecond = _runningTotal / 10;
		}
	}

	public override string ToString()
	{
		return Format(Packets, Bytes, BytesPerSecond);
	}

	public static void Combine(out uint packets, out uint bytes, out uint totalBytesPerSecond, [NotNull] params TrafficCounter[] counters)
	{
		packets = 0u;
		bytes = 0u;
		totalBytesPerSecond = 0u;
		foreach (TrafficCounter trafficCounter in counters)
		{
			if (trafficCounter != null)
			{
				packets += trafficCounter.Packets;
				bytes += trafficCounter.Bytes;
				totalBytesPerSecond += trafficCounter.BytesPerSecond;
			}
		}
	}

	[NotNull]
	public static string Format(ulong packets, ulong bytes, ulong bytesPerSecond)
	{
		return $"{FormatByteString(bytes)} in {packets:N0}pkts at {FormatByteString(bytesPerSecond)}/s";
	}

	[NotNull]
	private static string FormatByteString(decimal bytes)
	{
		string arg;
		if (bytes >= 1073741824m)
		{
			bytes /= 1073741824m;
			arg = "GiB";
		}
		else if (bytes >= 1048576m)
		{
			bytes /= 1048576m;
			arg = "MiB";
		}
		else if (bytes >= 1024m)
		{
			bytes /= 1024m;
			arg = "KiB";
		}
		else
		{
			arg = "B";
		}
		return $"{bytes:0.0}{arg}";
	}
}
