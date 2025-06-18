using System;
using System.Collections.Generic;
using System.Threading;
using Dissonance.Config;
using Dissonance.Threading;
using Dissonance.VAD;
using JetBrains.Annotations;
using NAudio.Wave;

namespace Dissonance.Audio.Capture;

internal abstract class BasePreprocessingPipeline : IPreprocessingPipeline, IDisposable, IMicrophoneSubscriber
{
	private static readonly Log Log = Logs.Create(LogCategory.Recording, typeof(BasePreprocessingPipeline).Name);

	private ArvCalculator _arv;

	private int _droppedSamples;

	private readonly object _inputWriteLock = new object();

	private readonly BufferedSampleProvider _resamplerInput;

	private readonly Resampler _resampler;

	private readonly SampleToFrameProvider _resampledOutput;

	private readonly float[] _intermediateFrame;

	private AudioFileWriter _diagnosticOutputRecorder;

	private readonly int _outputFrameSize;

	private readonly WaveFormat _outputFormat;

	private bool _resetApplied;

	private int _resetRequested;

	private volatile bool _runThread;

	private readonly DThread _thread;

	private readonly AutoResetEvent _threadEvent;

	private readonly ReadonlyLockedValue<List<IMicrophoneSubscriber>> _micSubscriptions = new ReadonlyLockedValue<List<IMicrophoneSubscriber>>(new List<IMicrophoneSubscriber>());

	private int _micSubscriptionCount;

	private readonly ReadonlyLockedValue<List<IVoiceActivationListener>> _vadSubscriptions = new ReadonlyLockedValue<List<IVoiceActivationListener>>(new List<IVoiceActivationListener>());

	private int _vadSubscriptionCount;

	private int _upstreamLatencyMs;

	private readonly int _estimatedPreprocessorLatencyMs;

	public float Amplitude => _arv.ARV;

	public int OutputFrameSize => _outputFrameSize;

	public abstract bool IsOutputMuted { set; }

	[NotNull]
	public WaveFormat OutputFormat => _outputFormat;

	protected abstract bool VadIsSpeechDetected { get; }

	public TimeSpan UpstreamLatency
	{
		get
		{
			return TimeSpan.FromMilliseconds((double)_upstreamLatencyMs);
		}
		set
		{
			_upstreamLatencyMs = (int)value.TotalMilliseconds;
		}
	}

	protected int PreprocessorLatencyMs => _upstreamLatencyMs + _estimatedPreprocessorLatencyMs;

	protected BasePreprocessingPipeline([NotNull] WaveFormat inputFormat, int intermediateFrameSize, int intermediateSampleRate, int outputFrameSize, int outputSampleRate)
	{
		if (inputFormat == null)
		{
			throw new ArgumentNullException("inputFormat");
		}
		if (intermediateFrameSize < 0)
		{
			throw new ArgumentOutOfRangeException("intermediateFrameSize", "Intermediate frame size cannot be less than zero");
		}
		if (intermediateSampleRate < 0)
		{
			throw new ArgumentOutOfRangeException("intermediateSampleRate", "Intermediate sample rate cannot be less than zero");
		}
		if (outputFrameSize < 0)
		{
			throw new ArgumentOutOfRangeException("outputFrameSize", "Output frame size cannot be less than zero");
		}
		if (outputSampleRate < 0)
		{
			throw new ArgumentOutOfRangeException("outputSampleRate", "Output sample rate cannot be less than zero");
		}
		_outputFrameSize = outputFrameSize;
		_outputFormat = new WaveFormat(outputSampleRate, 1);
		_resamplerInput = new BufferedSampleProvider(inputFormat, intermediateFrameSize * 16);
		_resampler = new Resampler(_resamplerInput, 48000);
		_resampledOutput = new SampleToFrameProvider(_resampler, (uint)OutputFrameSize);
		_intermediateFrame = new float[intermediateFrameSize];
		_threadEvent = new AutoResetEvent(initialState: false);
		_thread = new DThread(ThreadEntry);
		_estimatedPreprocessorLatencyMs = 0;
	}

	public virtual void Dispose()
	{
		_runThread = false;
		_threadEvent.Set();
		if (_thread.IsStarted)
		{
			_thread.Join();
		}
		if (_diagnosticOutputRecorder != null)
		{
			_diagnosticOutputRecorder.Dispose();
			_diagnosticOutputRecorder = null;
		}
		using (ReadonlyLockedValue<List<IMicrophoneSubscriber>>.Unlocker unlocker = _micSubscriptions.Lock())
		{
			while (unlocker.Value.Count > 0)
			{
				Unsubscribe(unlocker.Value[0]);
			}
		}
		using ReadonlyLockedValue<List<IVoiceActivationListener>>.Unlocker unlocker2 = _vadSubscriptions.Lock();
		while (unlocker2.Value.Count > 0)
		{
			Unsubscribe(unlocker2.Value[0]);
		}
	}

	public void Reset()
	{
		Interlocked.Exchange(ref _resetRequested, 1);
		_threadEvent.Set();
	}

	protected virtual void ApplyReset()
	{
		lock (_inputWriteLock)
		{
			_resamplerInput.Reset();
		}
		_resampler.Reset();
		_resampledOutput.Reset();
		_arv.Reset();
		_droppedSamples = 0;
		SendResetToSubscribers();
		_resetApplied = true;
	}

	void IMicrophoneSubscriber.ReceiveMicrophoneData(ArraySegment<float> data, [NotNull] WaveFormat format)
	{
		if (data.Array == null)
		{
			throw new ArgumentNullException("data");
		}
		if (!format.Equals(_resamplerInput.WaveFormat))
		{
			throw new ArgumentException("Incorrect format supplied to preprocessor", "format");
		}
		lock (_inputWriteLock)
		{
			int num = _resamplerInput.Write(data);
			if (num < data.Count)
			{
				int num2 = data.Count - num;
				Interlocked.Add(ref _droppedSamples, num2);
				Log.Warn("Lost {0} samples in the preprocessor (buffer full), injecting silence to compensate", num2);
			}
		}
		_threadEvent.Set();
	}

	public void Start()
	{
		try
		{
			_runThread = true;
			_thread.Start();
		}
		finally
		{
		}
	}

	private void ThreadEntry()
	{
		try
		{
			ThreadStart();
			ApplyReset();
			while (_runThread)
			{
				if (_resamplerInput.Count < _intermediateFrame.Length)
				{
					_threadEvent.WaitOne(100);
				}
				int tickCount = Environment.TickCount;
				if (Interlocked.Exchange(ref _resetRequested, 0) == 1 && !_resetApplied)
				{
					ApplyReset();
				}
				_resetApplied = false;
				int num = Interlocked.Exchange(ref _droppedSamples, 0);
				int num2 = 0;
				bool vadIsSpeechDetected = VadIsSpeechDetected;
				while (_resampledOutput.Read(new ArraySegment<float>(_intermediateFrame, 0, _intermediateFrame.Length)))
				{
					_arv.Update(new ArraySegment<float>(_intermediateFrame));
					PreprocessAudioFrame(_intermediateFrame);
					num2++;
				}
				bool vadIsSpeechDetected2 = VadIsSpeechDetected;
				if (vadIsSpeechDetected ^ vadIsSpeechDetected2)
				{
					if (vadIsSpeechDetected)
					{
						SendStoppedTalking();
					}
					else
					{
						SendStartedTalking();
					}
				}
				if (num > 0)
				{
					Array.Clear(_intermediateFrame, 0, _intermediateFrame.Length);
					while (num >= _intermediateFrame.Length)
					{
						PreprocessAudioFrame(_intermediateFrame);
						num -= _intermediateFrame.Length;
					}
					if (num > 0)
					{
						Interlocked.Add(ref _droppedSamples, num);
					}
				}
				int tickCount2 = Environment.TickCount;
				int num3 = tickCount2 - tickCount;
				if (tickCount2 > tickCount && num3 > 50 + num2 * 5)
				{
					Log.Warn("Preprocessor running slow! Iteration took:{0}ms for {1} frames", num3, num2);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error(Log.PossibleBugMessage("Unhandled exception killed audio preprocessor thread: " + ex, "02EB75C0-1E12-4109-BFD2-64645C14BD5F"));
		}
	}

	protected virtual void ThreadStart()
	{
	}

	protected abstract void PreprocessAudioFrame([NotNull] float[] frame);

	protected void SendSamplesToSubscribers([NotNull] float[] buffer)
	{
		if (DebugSettings.Instance.EnableRecordingDiagnostics && DebugSettings.Instance.RecordPreprocessorOutput)
		{
			if (_diagnosticOutputRecorder == null)
			{
				string filename = $"Dissonance_Diagnostics/PreprocessorOutputAudio_{DateTime.UtcNow.ToFileTime()}";
				_diagnosticOutputRecorder = new AudioFileWriter(filename, OutputFormat);
			}
			_diagnosticOutputRecorder.WriteSamples(new ArraySegment<float>(buffer));
		}
		using ReadonlyLockedValue<List<IMicrophoneSubscriber>>.Unlocker unlocker = _micSubscriptions.Lock();
		List<IMicrophoneSubscriber> value = unlocker.Value;
		for (int i = 0; i < value.Count; i++)
		{
			try
			{
				value[i].ReceiveMicrophoneData(new ArraySegment<float>(buffer), OutputFormat);
			}
			catch (Exception p)
			{
				Log.Error("Microphone subscriber '{0}' threw: {1}", value[i].GetType().Name, p);
			}
		}
	}

	private void SendResetToSubscribers()
	{
		using ReadonlyLockedValue<List<IMicrophoneSubscriber>>.Unlocker unlocker = _micSubscriptions.Lock();
		List<IMicrophoneSubscriber> value = unlocker.Value;
		for (int i = 0; i < value.Count; i++)
		{
			try
			{
				value[i].Reset();
			}
			catch (Exception p)
			{
				Log.Error("Microphone subscriber '{0}' Reset threw: {1}", value[i].GetType().Name, p);
			}
		}
	}

	public virtual void Subscribe([NotNull] IMicrophoneSubscriber listener)
	{
		if (listener == null)
		{
			throw new ArgumentNullException("listener");
		}
		using ReadonlyLockedValue<List<IMicrophoneSubscriber>>.Unlocker unlocker = _micSubscriptions.Lock();
		unlocker.Value.Add(listener);
		Interlocked.Increment(ref _micSubscriptionCount);
	}

	public virtual bool Unsubscribe([NotNull] IMicrophoneSubscriber listener)
	{
		if (listener == null)
		{
			throw new ArgumentNullException("listener");
		}
		using ReadonlyLockedValue<List<IMicrophoneSubscriber>>.Unlocker unlocker = _micSubscriptions.Lock();
		bool num = unlocker.Value.Remove(listener);
		if (num)
		{
			Interlocked.Decrement(ref _micSubscriptionCount);
		}
		return num;
	}

	private void SendStoppedTalking()
	{
		using ReadonlyLockedValue<List<IVoiceActivationListener>>.Unlocker unlocker = _vadSubscriptions.Lock();
		List<IVoiceActivationListener> value = unlocker.Value;
		for (int i = 0; i < value.Count; i++)
		{
			SendStoppedTalking(value[i]);
		}
	}

	private static void SendStoppedTalking([NotNull] IVoiceActivationListener listener)
	{
		try
		{
			listener.VoiceActivationStop();
		}
		catch (Exception p)
		{
			Log.Error("VAD subscriber '{0}' threw: {1}", listener.GetType().Name, p);
		}
	}

	private void SendStartedTalking()
	{
		using ReadonlyLockedValue<List<IVoiceActivationListener>>.Unlocker unlocker = _vadSubscriptions.Lock();
		List<IVoiceActivationListener> value = unlocker.Value;
		for (int i = 0; i < value.Count; i++)
		{
			SendStartedTalking(value[i]);
		}
	}

	private static void SendStartedTalking([NotNull] IVoiceActivationListener listener)
	{
		try
		{
			listener.VoiceActivationStart();
		}
		catch (Exception p)
		{
			Log.Error("VAD subscriber '{0}' threw: {1}", listener.GetType().Name, p);
		}
	}

	public virtual void Subscribe([NotNull] IVoiceActivationListener listener)
	{
		if (listener == null)
		{
			throw new ArgumentNullException("listener");
		}
		using ReadonlyLockedValue<List<IVoiceActivationListener>>.Unlocker unlocker = _vadSubscriptions.Lock();
		unlocker.Value.Add(listener);
		Interlocked.Increment(ref _vadSubscriptionCount);
		if (VadIsSpeechDetected)
		{
			SendStartedTalking(listener);
		}
	}

	public virtual bool Unsubscribe([NotNull] IVoiceActivationListener listener)
	{
		if (listener == null)
		{
			throw new ArgumentNullException("listener");
		}
		using ReadonlyLockedValue<List<IVoiceActivationListener>>.Unlocker unlocker = _vadSubscriptions.Lock();
		bool num = unlocker.Value.Remove(listener);
		if (num)
		{
			Interlocked.Decrement(ref _vadSubscriptionCount);
			if (VadIsSpeechDetected)
			{
				SendStoppedTalking(listener);
			}
		}
		return num;
	}
}
