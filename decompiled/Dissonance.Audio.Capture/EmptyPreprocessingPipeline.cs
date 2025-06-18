using JetBrains.Annotations;
using NAudio.Wave;

namespace Dissonance.Audio.Capture;

internal class EmptyPreprocessingPipeline : BasePreprocessingPipeline
{
	public override bool IsOutputMuted
	{
		set
		{
		}
	}

	protected override bool VadIsSpeechDetected => true;

	public EmptyPreprocessingPipeline([NotNull] WaveFormat inputFormat)
		: base(inputFormat, 480, 48000, 480, 48000)
	{
	}

	protected override void PreprocessAudioFrame(float[] frame)
	{
		SendSamplesToSubscribers(frame);
	}
}
