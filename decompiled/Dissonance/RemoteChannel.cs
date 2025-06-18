using System;
using Dissonance.Audio.Playback;
using JetBrains.Annotations;

namespace Dissonance;

public struct RemoteChannel
{
	private readonly string _target;

	private readonly ChannelType _type;

	private readonly PlaybackOptions _options;

	public ChannelType Type => _type;

	public PlaybackOptions Options => _options;

	public string TargetName => _target;

	internal RemoteChannel([NotNull] string targetName, ChannelType type, PlaybackOptions options)
	{
		if (targetName == null)
		{
			throw new ArgumentNullException("targetName");
		}
		_target = targetName;
		_type = type;
		_options = options;
	}
}
