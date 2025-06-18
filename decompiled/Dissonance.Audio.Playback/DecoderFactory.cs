using System;
using Dissonance.Audio.Codecs;
using Dissonance.Audio.Codecs.Identity;
using Dissonance.Audio.Codecs.Opus;
using Dissonance.Audio.Codecs.Silence;
using Dissonance.Config;
using JetBrains.Annotations;

namespace Dissonance.Audio.Playback;

internal class DecoderFactory
{
	private static readonly Log Log = Logs.Create(LogCategory.Playback, typeof(DecoderFactory).Name);

	[NotNull]
	public static IVoiceDecoder Create(FrameFormat format)
	{
		try
		{
			return format.Codec switch
			{
				Codec.Identity => new IdentityDecoder(format.WaveFormat), 
				Codec.Opus => new OpusDecoder(format.WaveFormat, VoiceSettings.Instance.ForwardErrorCorrection), 
				_ => throw new ArgumentOutOfRangeException("format", "Unknown codec."), 
			};
		}
		catch (Exception p)
		{
			Log.Error("Encountered unexpected error creating decoder. Audio playback will be disabled.\n{0}", p);
			return new SilenceDecoder(format);
		}
	}
}
