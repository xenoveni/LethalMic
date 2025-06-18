using System.Threading;
using JetBrains.Annotations;

namespace Dissonance;

public static class Metrics
{
	private struct MetricEvent
	{
		public readonly string Name;

		public readonly double Value;

		public MetricEvent(string name, double value)
		{
			Name = name;
			Value = value;
		}
	}

	private static readonly Log Log = Logs.Create(LogCategory.Core, typeof(Metrics).Name);

	private static Thread _main;

	internal static void WriteMultithreadedMetrics()
	{
		if (_main == null)
		{
			_main = Thread.CurrentThread;
		}
	}

	private static void InternalSampleMetric(string name, double value)
	{
	}

	[CanBeNull]
	public static string MetricName(string category, string id)
	{
		return null;
	}

	[CanBeNull]
	public static string MetricName(string category)
	{
		return null;
	}

	public static void Sample([CanBeNull] string name, float value)
	{
	}
}
