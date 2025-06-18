using System;
using System.Collections.Generic;
using System.Linq;
using Dissonance.Config;
using Dissonance.Datastructures;
using JetBrains.Annotations;
using NAudio.Wave;
using UnityEngine;

namespace Dissonance.Audio.Capture;

public class BasicMicrophoneCapture : MonoBehaviour, IMicrophoneCapture, IMicrophoneDeviceList
{
	private static readonly Log Log = Logs.Create(LogCategory.Recording, typeof(BasicMicrophoneCapture).Name);

	private byte _maxReadBufferPower;

	private readonly POTBuffer _readBuffer = new POTBuffer(10);

	private BufferedSampleProvider _rawMicSamples;

	private IFrameProvider _rawMicFrames;

	private float[] _frame;

	private WaveFormat _format;

	private AudioClip _clip;

	private int _readHead;

	private bool _started;

	private string _micName;

	private bool _audioDeviceChanged;

	private AudioFileWriter _microphoneDiagnosticOutput;

	private readonly List<IMicrophoneSubscriber> _subscribers = new List<IMicrophoneSubscriber>();

	public string Device => _micName;

	public TimeSpan Latency { get; private set; }

	public bool IsRecording => (Object)(object)_clip != (Object)null;

	public virtual WaveFormat StartCapture(string inputMicName)
	{
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Expected O, but got Unknown
		_micName = null;
		try
		{
			Log.AssertAndThrowPossibleBug((Object)(object)_clip == (Object)null, "1BAD3E74-B451-4B7D-A9B9-35225BE55364", "Attempted to Start microphone capture, but capture is already running");
			if (Log.AssertAndLogWarn(Microphone.devices.Length != 0, "No microphone detected; disabling voice capture"))
			{
				return null;
			}
			_micName = ChooseMicName(inputMicName);
			int num3;
			try
			{
				int num = default(int);
				int num2 = default(int);
				Microphone.GetDeviceCaps(_micName, ref num, ref num2);
				num3 = ((num == 0 && num2 == 0) ? 48000 : Mathf.Clamp(48000, num, num2));
			}
			finally
			{
			}
			try
			{
				if ((Object)(object)_clip != (Object)null)
				{
					Object.Destroy((Object)(object)_clip);
					_clip = null;
				}
				_clip = Microphone.Start(_micName, true, 10, num3);
				if ((Object)(object)_clip == (Object)null)
				{
					Log.Error("Failed to start microphone capture");
					_micName = null;
					return null;
				}
			}
			finally
			{
			}
			_format = new WaveFormat(_clip.frequency, 1);
			_maxReadBufferPower = (byte)Math.Ceiling(Math.Log(0.1f * (float)_clip.frequency, 2.0));
			int num4 = (int)(0.02 * (double)_clip.frequency);
			if (_rawMicSamples == null || _rawMicSamples.WaveFormat != _format || _rawMicSamples.Capacity != num4 || _rawMicFrames.FrameSize != num4)
			{
				_rawMicSamples = new BufferedSampleProvider(_format, num4 * 4);
				_rawMicFrames = new SampleToFrameProvider(_rawMicSamples, (uint)num4);
			}
			if (_frame == null || _frame.Length != num4)
			{
				_frame = new float[num4];
			}
			AudioSettings.OnAudioConfigurationChanged += new AudioConfigurationChangeHandler(OnAudioDeviceChanged);
			_audioDeviceChanged = false;
			for (int i = 0; i < _subscribers.Count; i++)
			{
				_subscribers[i].Reset();
			}
			Latency = TimeSpan.FromSeconds((float)num4 / (float)_format.SampleRate);
			Log.Info("Began mic capture (SampleRate:{0}Hz, FrameSize:{1}, Buffer Limit:2^{2}, Latency:{3}ms, Device:'{4}')", _clip.frequency, num4, _maxReadBufferPower, Latency.TotalMilliseconds, _micName);
			return _format;
		}
		finally
		{
		}
	}

	[CanBeNull]
	private static string ChooseMicName([CanBeNull] string micName)
	{
		if (string.IsNullOrEmpty(micName))
		{
			return null;
		}
		if (!Microphone.devices.Contains(micName))
		{
			Log.Warn("Cannot find microphone '{0}', using default mic", micName);
			return null;
		}
		return micName;
	}

	private void OnDestroy()
	{
		if ((Object)(object)_clip != (Object)null)
		{
			Object.Destroy((Object)(object)_clip);
			_clip = null;
		}
	}

	public virtual void StopCapture()
	{
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Expected O, but got Unknown
		Log.AssertAndThrowPossibleBug((Object)(object)_clip != (Object)null, "CDDAE69D-44DC-487F-9B69-4703B779400E", "Attempted to stop microphone capture, but it is already stopped");
		if (_microphoneDiagnosticOutput != null)
		{
			_microphoneDiagnosticOutput.Dispose();
			_microphoneDiagnosticOutput = null;
		}
		Microphone.End(_micName);
		_format = null;
		_readHead = 0;
		_started = false;
		_micName = null;
		if ((Object)(object)_clip != (Object)null)
		{
			Object.Destroy((Object)(object)_clip);
			_clip = null;
		}
		_rawMicSamples.Reset();
		_rawMicFrames.Reset();
		AudioSettings.OnAudioConfigurationChanged -= new AudioConfigurationChangeHandler(OnAudioDeviceChanged);
		_audioDeviceChanged = false;
	}

	private void OnAudioDeviceChanged(bool deviceWasChanged)
	{
		_audioDeviceChanged |= deviceWasChanged;
	}

	public bool UpdateSubscribers()
	{
		if (!_started)
		{
			_readHead = Microphone.GetPosition(_micName);
			_started = _readHead > 0;
			if (!_started)
			{
				return false;
			}
		}
		if (_clip.samples == 0)
		{
			Log.Error("Unknown microphone capture error (zero length clip) - restarting mic");
			return true;
		}
		if (_audioDeviceChanged)
		{
			return true;
		}
		if (!Microphone.IsRecording(_micName))
		{
			Log.Warn("Microphone stopped recording for an unknown reason (possibly due to an external script calling `Microphone.End`");
			return true;
		}
		if (_subscribers.Count > 0)
		{
			DrainMicSamples();
		}
		else
		{
			_readHead = Microphone.GetPosition(_micName);
			_rawMicSamples.Reset();
			_rawMicFrames.Reset();
			if (_microphoneDiagnosticOutput != null)
			{
				_microphoneDiagnosticOutput.Dispose();
				_microphoneDiagnosticOutput = null;
			}
		}
		return false;
	}

	private void DrainMicSamples()
	{
		int position = Microphone.GetPosition(_micName);
		uint count = (uint)((_clip.samples + position - _readHead) % _clip.samples);
		if (count == 0)
		{
			return;
		}
		while (count > _readBuffer.MaxCount)
		{
			if (_readBuffer.Pow2 > _maxReadBufferPower || !_readBuffer.Expand())
			{
				float num = Mathf.Min((float)_clip.samples, (float)(count - _readBuffer.MaxCount));
				Log.Warn("Insufficient buffer space, requested {0}, clamped to {1} (dropping {2} samples)", count, _readBuffer.MaxCount, num);
				count = _readBuffer.MaxCount;
				_readHead = (int)(((float)_readHead + num) % (float)_clip.samples);
				break;
			}
		}
		_readBuffer.Alloc(count);
		try
		{
			while (count != 0)
			{
				float[] buffer = _readBuffer.GetBuffer(ref count, zeroed: true);
				_clip.GetData(buffer, _readHead);
				_readHead = (_readHead + buffer.Length) % _clip.samples;
				ConsumeSamples(new ArraySegment<float>(buffer, 0, buffer.Length));
			}
		}
		finally
		{
			_readBuffer.Free();
		}
	}

	private void ConsumeSamples(ArraySegment<float> samples)
	{
		if (samples.Array == null)
		{
			throw new ArgumentNullException("samples");
		}
		while (samples.Count > 0)
		{
			int num = _rawMicSamples.Write(samples);
			samples = new ArraySegment<float>(samples.Array, samples.Offset + num, samples.Count - num);
			SendFrame();
		}
	}

	private void SendFrame()
	{
		while (_rawMicSamples.Count > _rawMicFrames.FrameSize)
		{
			ArraySegment<float> arraySegment = new ArraySegment<float>(_frame);
			if (!_rawMicFrames.Read(arraySegment))
			{
				break;
			}
			if (DebugSettings.Instance.EnableRecordingDiagnostics && DebugSettings.Instance.RecordMicrophoneRawAudio)
			{
				if (_microphoneDiagnosticOutput == null)
				{
					string filename = $"Dissonance_Diagnostics/MicrophoneRawAudio_{DateTime.UtcNow.ToFileTime()}";
					_microphoneDiagnosticOutput = new AudioFileWriter(filename, _format);
				}
			}
			else if (_microphoneDiagnosticOutput != null)
			{
				_microphoneDiagnosticOutput.Dispose();
				_microphoneDiagnosticOutput = null;
			}
			if (_microphoneDiagnosticOutput != null)
			{
				_microphoneDiagnosticOutput.WriteSamples(arraySegment);
				_microphoneDiagnosticOutput.Flush();
			}
			for (int i = 0; i < _subscribers.Count; i++)
			{
				_subscribers[i].ReceiveMicrophoneData(arraySegment, _format);
			}
		}
	}

	public void Subscribe(IMicrophoneSubscriber listener)
	{
		if (listener == null)
		{
			throw new ArgumentNullException("listener");
		}
		_subscribers.Add(listener);
	}

	public bool Unsubscribe(IMicrophoneSubscriber listener)
	{
		if (listener == null)
		{
			throw new ArgumentNullException("listener");
		}
		return _subscribers.Remove(listener);
	}

	public void GetDevices([NotNull] List<string> output)
	{
		output.AddRange(Microphone.devices);
	}
}
