using System;
using NAudio.Dsp;
using NAudio.Wave;
using UnityEngine;

namespace Dissonance.Audio.Playback;

internal class Resampler : ISampleSource
{
	private static readonly Log Log = Logs.Create(LogCategory.Playback, typeof(Resampler).Name);

	private readonly ISampleSource _source;

	private readonly IRateProvider _rate;

	private volatile WaveFormat _outputFormat;

	private readonly WdlResampler _resampler;

	public WaveFormat WaveFormat => _outputFormat;

	public Resampler(ISampleSource source, IRateProvider rate)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Expected O, but got Unknown
		_source = source;
		_rate = rate;
		AudioSettings.OnAudioConfigurationChanged += new AudioConfigurationChangeHandler(OnAudioConfigurationChanged);
		OnAudioConfigurationChanged(deviceWasChanged: false);
		_resampler = new WdlResampler();
		_resampler.SetMode(interp: true, 2, sinc: false);
		_resampler.SetFilterParms();
		_resampler.SetFeedMode(wantInputDriven: false);
	}

	public void Prepare(SessionContext context)
	{
		_source.Prepare(context);
	}

	public bool Read(ArraySegment<float> samples)
	{
		WaveFormat waveFormat = _source.WaveFormat;
		WaveFormat outputFormat = _outputFormat;
		double num = outputFormat.SampleRate;
		if (Mathf.Abs(_rate.PlaybackRate - 1f) > 0.01f)
		{
			num = (float)outputFormat.SampleRate * (1f / _rate.PlaybackRate);
		}
		if (num != _resampler.OutputSampleRate)
		{
			_resampler.SetRates(waveFormat.SampleRate, num);
		}
		int channels = waveFormat.Channels;
		int num2 = samples.Count / channels;
		float[] inbuffer;
		int inbufferOffset;
		int num3 = _resampler.ResamplePrepare(num2, channels, out inbuffer, out inbufferOffset);
		ArraySegment<float> samples2 = new ArraySegment<float>(inbuffer, inbufferOffset, num3 * channels);
		bool result = _source.Read(samples2);
		_resampler.ResampleOut(samples.Array, samples.Offset, num3, num2, channels);
		return result;
	}

	public void Reset()
	{
		if (_resampler != null)
		{
			_resampler.Reset();
		}
		_source.Reset();
	}

	private void OnAudioConfigurationChanged(bool deviceWasChanged)
	{
		_outputFormat = new WaveFormat(AudioSettings.outputSampleRate, _source.WaveFormat.Channels);
	}
}
