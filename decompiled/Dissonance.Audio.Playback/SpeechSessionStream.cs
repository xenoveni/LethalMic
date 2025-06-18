using System;
using System.Collections.Generic;
using Dissonance.Datastructures;
using Dissonance.Networking;
using JetBrains.Annotations;

namespace Dissonance.Audio.Playback;

internal class SpeechSessionStream : IJitterEstimator
{
	private static readonly Log Log = Logs.Create(LogCategory.Playback, typeof(SpeechSessionStream).Name);

	private string _metricArrivalDelay;

	private readonly Queue<SpeechSession> _awaitingActivation;

	private readonly IVolumeProvider _volumeProvider;

	private DateTime? _queueHeadFirstDequeueAttempt;

	private DecoderPipeline _active;

	private uint _currentId;

	private string _playerName;

	private readonly WindowDeviationCalculator _arrivalJitterMeter = new WindowDeviationCalculator(128u);

	public string PlayerName
	{
		get
		{
			return _playerName;
		}
		set
		{
			if (_playerName != value)
			{
				_metricArrivalDelay = Metrics.MetricName("PacketArrivalDelay", _playerName);
				_playerName = value;
				_arrivalJitterMeter.Clear();
			}
		}
	}

	float IJitterEstimator.Jitter => _arrivalJitterMeter.StdDev;

	float IJitterEstimator.Confidence => _arrivalJitterMeter.Confidence;

	public SpeechSessionStream(IVolumeProvider volumeProvider)
	{
		_volumeProvider = volumeProvider;
		_awaitingActivation = new Queue<SpeechSession>();
	}

	public void StartSession(FrameFormat format, DateTime? now = null, [CanBeNull] IJitterEstimator jitter = null)
	{
		if (PlayerName == null)
		{
			throw Log.CreatePossibleBugException("Attempted to `StartSession` but `PlayerName` is null", "0C0F3731-8D6B-43F6-87C1-33CEC7A26804");
		}
		_active = DecoderPipelinePool.GetDecoderPipeline(format, _volumeProvider);
		SpeechSession item = SpeechSession.Create(new SessionContext(PlayerName, _currentId++), jitter ?? this, _active, _active, now ?? DateTime.UtcNow);
		_awaitingActivation.Enqueue(item);
	}

	public SpeechSession? TryDequeueSession(DateTime? now = null)
	{
		DateTime dateTime = now ?? DateTime.UtcNow;
		if (_awaitingActivation.Count > 0)
		{
			if (!_queueHeadFirstDequeueAttempt.HasValue)
			{
				_queueHeadFirstDequeueAttempt = dateTime;
			}
			SpeechSession value = _awaitingActivation.Peek();
			if (value.TargetActivationTime < dateTime)
			{
				value.Prepare(_queueHeadFirstDequeueAttempt.Value);
				_awaitingActivation.Dequeue();
				_queueHeadFirstDequeueAttempt = null;
				return value;
			}
		}
		return null;
	}

	public void ReceiveFrame(VoicePacket packet, DateTime? now = null)
	{
		if (packet.SenderPlayerId != PlayerName)
		{
			throw Log.CreatePossibleBugException($"Attempted to deliver voice from player {packet.SenderPlayerId} to playback queue for player {PlayerName}", "F55DB7D5-621B-4F5B-8C19-700B1FBC9871");
		}
		if (_active == null)
		{
			Log.Warn(Log.PossibleBugMessage($"Attempted to deliver voice from player {packet.SenderPlayerId} with no active session", "1BD954EC-B455-421F-9D6E-2E3D087BC0A9"));
			return;
		}
		float num = _active.Push(packet, now ?? DateTime.UtcNow);
		Metrics.Sample(_metricArrivalDelay, num);
		_arrivalJitterMeter.Update(num);
	}

	public void ForceReset()
	{
		if (_active != null)
		{
			_active.Reset();
		}
		while (_awaitingActivation.Count > 1)
		{
			_awaitingActivation.Dequeue();
		}
		if (_active == null && _awaitingActivation.Count > 0)
		{
			_awaitingActivation.Dequeue();
		}
		_arrivalJitterMeter.Clear();
	}

	public void StopSession(bool logNoSessionError = true)
	{
		if (_active != null)
		{
			_active.Stop();
			_active = null;
		}
		else if (logNoSessionError)
		{
			Log.Warn(Log.PossibleBugMessage("Attempted to stop a session, but there is no active session", "6DB702AA-D683-47AA-9544-BE4857EF8160"));
		}
	}
}
