using System;

namespace Dissonance.Audio.Codecs.Opus;

internal static class BandwidthExtensions
{
	private static readonly Log Log = Logs.Create(LogCategory.Core, typeof(BandwidthExtensions).Name);

	public static int SampleRate(this OpusNative.Bandwidth bandwidth)
	{
		return bandwidth switch
		{
			OpusNative.Bandwidth.Narrowband => 8000, 
			OpusNative.Bandwidth.Mediumband => 12000, 
			OpusNative.Bandwidth.Wideband => 16000, 
			OpusNative.Bandwidth.SuperWideband => 24000, 
			OpusNative.Bandwidth.Fullband => 48000, 
			_ => throw new ArgumentOutOfRangeException("bandwidth", Log.PossibleBugMessage($"{bandwidth} is not a valid value", "B534C9B2-6A9B-455E-875E-A01D93B278C8")), 
		};
	}
}
