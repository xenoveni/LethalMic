using System;
using System.Collections.Generic;
using Dissonance.Datastructures;
using JetBrains.Annotations;

namespace Dissonance.Audio.Playback;

internal static class DecoderPipelinePool
{
	private static readonly Dictionary<FrameFormat, ConcurrentPool<DecoderPipeline>> Pools = new Dictionary<FrameFormat, ConcurrentPool<DecoderPipeline>>();

	private static int _nextPipelineId;

	[NotNull]
	private static ConcurrentPool<DecoderPipeline> GetPool(FrameFormat format)
	{
		if (!Pools.TryGetValue(format, out var value))
		{
			value = new ConcurrentPool<DecoderPipeline>(3, () => new DecoderPipeline(DecoderFactory.Create(format), id: _nextPipelineId.ToString(), inputFrameSize: format.FrameSize, completionHandler: delegate(DecoderPipeline p)
			{
				p.Reset();
				Recycle(format, p);
			}));
			Pools[format] = value;
			_nextPipelineId++;
		}
		return value;
	}

	[NotNull]
	internal static DecoderPipeline GetDecoderPipeline(FrameFormat format, [NotNull] IVolumeProvider volume)
	{
		if (volume == null)
		{
			throw new ArgumentNullException("volume");
		}
		DecoderPipeline decoderPipeline = GetPool(format).Get();
		decoderPipeline.Reset();
		decoderPipeline.VolumeProvider = volume;
		return decoderPipeline;
	}

	private static void Recycle(FrameFormat format, [CanBeNull] DecoderPipeline pipeline)
	{
		if (pipeline != null)
		{
			GetPool(format).Put(pipeline);
		}
	}
}
