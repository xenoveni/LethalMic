using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dissonance.Networking;
using Dissonance.VAD;
using JetBrains.Annotations;
using NAudio.Wave;
using UnityEngine;

namespace Dissonance.Audio.Capture;

internal class CapturePipelineManager : IAmplitudeProvider, ILossEstimator
{
	private static readonly Log Log = Logs.Create(LogCategory.Recording, typeof(CapturePipelineManager).Name);

	private bool _isMobilePlatform;

	private readonly CodecSettingsLoader _codecSettingsLoader;

	private readonly RoomChannels _roomChannels;

	private readonly PlayerChannels _playerChannels;

	private readonly PacketLossMonitor _receivingPacketLossMonitor;

	[CanBeNull]
	private ICommsNetwork _network;

	private IMicrophoneCapture _microphone;

	private IPreprocessingPipeline _preprocessor;

	private EncoderPipeline _encoder;

	private bool _encounteredFatalException;

	private bool _netModeRequiresPipeline;

	private bool _cannotStartMic;

	private bool _encoderSubscribed;

	private int _startupDelay;

	private FrameSkipDetector _skipDetector = new FrameSkipDetector(TimeSpan.FromMilliseconds(150.0), TimeSpan.FromMilliseconds(350.0), TimeSpan.FromMilliseconds(10000.0), TimeSpan.FromMilliseconds(250.0));

	private readonly List<IVoiceActivationListener> _activationListeners = new List<IVoiceActivationListener>();

	private readonly List<IMicrophoneSubscriber> _audioListeners = new List<IMicrophoneSubscriber>();

	private string _micName;

	private bool _pendingResetRequest;

	[CanBeNull]
	public IMicrophoneCapture Microphone => _microphone;

	public string MicrophoneName
	{
		get
		{
			return _micName;
		}
		set
		{
			if (!(_micName == value) && (!string.IsNullOrEmpty(_micName) || !string.IsNullOrEmpty(value)))
			{
				if (_microphone != null && _microphone.IsRecording)
				{
					Log.Info("Changing microphone device from '{0}' to '{1}'", _micName, value);
				}
				_micName = value;
				RestartTransmissionPipeline("Microphone name changed");
			}
		}
	}

	public float PacketLoss => _receivingPacketLossMonitor.PacketLoss;

	public float Amplitude
	{
		get
		{
			if (_preprocessor != null)
			{
				return _preprocessor.Amplitude;
			}
			return 0f;
		}
	}

	public CapturePipelineManager([NotNull] CodecSettingsLoader codecSettingsLoader, [NotNull] RoomChannels roomChannels, [NotNull] PlayerChannels playerChannels, [NotNull] ReadOnlyCollection<VoicePlayerState> players, int startupDelay = 0)
	{
		if (codecSettingsLoader == null)
		{
			throw new ArgumentNullException("codecSettingsLoader");
		}
		if (roomChannels == null)
		{
			throw new ArgumentNullException("roomChannels");
		}
		if (playerChannels == null)
		{
			throw new ArgumentNullException("playerChannels");
		}
		if (players == null)
		{
			throw new ArgumentNullException("players");
		}
		_codecSettingsLoader = codecSettingsLoader;
		_roomChannels = roomChannels;
		_playerChannels = playerChannels;
		_receivingPacketLossMonitor = new PacketLossMonitor(players);
		_startupDelay = startupDelay;
	}

	public void Start([NotNull] ICommsNetwork network, [NotNull] IMicrophoneCapture microphone)
	{
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Expected O, but got Unknown
		if (network == null)
		{
			throw new ArgumentNullException("network");
		}
		if (microphone == null)
		{
			throw new ArgumentNullException("microphone");
		}
		_microphone = microphone;
		_network = network;
		AudioSettingsWatcher.Instance.Start();
		Net_ModeChanged(network.Mode);
		network.ModeChanged += Net_ModeChanged;
		AudioSettings.OnAudioConfigurationChanged += new AudioConfigurationChangeHandler(OnAudioDeviceChanged);
		_isMobilePlatform = IsMobilePlatform();
	}

	private void OnAudioDeviceChanged(bool devicewaschanged)
	{
		if (devicewaschanged)
		{
			ForceReset();
		}
	}

	private static bool IsMobilePlatform()
	{
		_ = SystemInfo.deviceModel == "Oculus Quest";
		return false;
	}

	public void Destroy()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		if (_network != null)
		{
			_network.ModeChanged -= Net_ModeChanged;
		}
		AudioSettings.OnAudioConfigurationChanged -= new AudioConfigurationChangeHandler(OnAudioDeviceChanged);
		StopTransmissionPipeline();
	}

	private void Net_ModeChanged(NetworkMode mode)
	{
		_netModeRequiresPipeline = mode.IsClientEnabled();
	}

	public void Update(bool muted, float deltaTime)
	{
		_startupDelay--;
		if (_startupDelay > 0)
		{
			return;
		}
		_receivingPacketLossMonitor.Update();
		if (!_netModeRequiresPipeline || _encounteredFatalException || _cannotStartMic)
		{
			StopTransmissionPipeline();
			return;
		}
		bool flag = _skipDetector.IsFrameSkip(deltaTime);
		bool flag2 = _microphone.IsRecording && _microphone.UpdateSubscribers();
		bool flag3 = _netModeRequiresPipeline && _encoder == null;
		if (flag || flag2 || _pendingResetRequest || flag3)
		{
			string text = (flag ? "Detected a frame skip, forcing capture pipeline reset" : (flag3 ? "Network mode changed" : (_pendingResetRequest ? "Applying external reset request" : "Microphone requested a pipeline reset")));
			if (flag)
			{
				if (Log.IsWarn)
				{
					text = $"Detected a frame skip, forcing capture pipeline reset (Delta Time:{deltaTime})";
				}
				Log.Warn(text);
			}
			RestartTransmissionPipeline(text);
			if (_preprocessor == null || _cannotStartMic)
			{
				return;
			}
		}
		_preprocessor.IsOutputMuted = !_encoderSubscribed;
		if (_encoder == null)
		{
			return;
		}
		if (_encoder.Stopped && _encoderSubscribed)
		{
			_preprocessor.Unsubscribe(_encoder);
			_encoder.Reset();
			_encoderSubscribed = false;
		}
		bool flag4 = (!_encoder.Stopping || _encoder.Stopped) && !muted && _roomChannels.Count + _playerChannels.Count > 0;
		if (flag4 != _encoderSubscribed)
		{
			if (flag4)
			{
				_encoder.Reset();
				_preprocessor.Subscribe(_encoder);
				_encoderSubscribed = true;
			}
			else if (!_encoder.Stopping)
			{
				_encoder.Stop();
			}
		}
		if (_encoder != null)
		{
			_encoder.TransmissionPacketLoss = _receivingPacketLossMonitor.PacketLoss;
		}
	}

	private void StopTransmissionPipeline()
	{
		if (_microphone != null && _microphone.IsRecording)
		{
			_microphone.StopCapture();
		}
		if (_preprocessor != null)
		{
			if (_microphone != null)
			{
				_microphone.Unsubscribe(_preprocessor);
			}
			if (_encoder != null)
			{
				_preprocessor.Unsubscribe(_encoder);
			}
			_preprocessor.Dispose();
			_preprocessor = null;
		}
		if (_encoder != null)
		{
			_encoder.Dispose();
			_encoder = null;
		}
		_encoderSubscribed = false;
	}

	private void RestartTransmissionPipeline(string reason)
	{
		StopTransmissionPipeline();
		if (_encounteredFatalException)
		{
			return;
		}
		try
		{
			_pendingResetRequest = false;
			if (_network == null || !_network.Mode.IsClientEnabled())
			{
				return;
			}
			WaveFormat waveFormat = _microphone.StartCapture(_micName);
			if (waveFormat != null)
			{
				_roomChannels.Refresh();
				_playerChannels.Refresh();
				_preprocessor = CreatePreprocessor(waveFormat);
				_preprocessor.UpstreamLatency = _microphone.Latency;
				_preprocessor.Start();
				_microphone.Subscribe(_preprocessor);
				for (int i = 0; i < _activationListeners.Count; i++)
				{
					_preprocessor.Subscribe(_activationListeners[i]);
				}
				for (int j = 0; j < _audioListeners.Count; j++)
				{
					IMicrophoneSubscriber microphoneSubscriber = _audioListeners[j];
					microphoneSubscriber.Reset();
					_preprocessor.Subscribe(microphoneSubscriber);
				}
				Log.AssertAndThrowPossibleBug(_network != null, "5F33336B-15B5-4A85-9B54-54352C74768E", "Network object is unexpectedly null");
				_encoder = new EncoderPipeline(_preprocessor.OutputFormat, _codecSettingsLoader.CreateEncoder(), _network);
			}
			else
			{
				Log.Warn("Failed to start microphone capture; local voice transmission will be disabled.");
				_cannotStartMic = true;
			}
		}
		catch (Exception p)
		{
			StopTransmissionPipeline();
			Log.Error("Unexpected exception encountered starting microphone capture; local voice transmission will be disabled: {0}", p);
			_encounteredFatalException = true;
		}
		finally
		{
		}
	}

	[NotNull]
	protected virtual IPreprocessingPipeline CreatePreprocessor([NotNull] WaveFormat format)
	{
		return new WebRtcPreprocessingPipeline(format, _isMobilePlatform);
	}

	public void Subscribe([NotNull] IVoiceActivationListener listener)
	{
		if (listener == null)
		{
			throw new ArgumentNullException("listener", "Cannot subscribe with a null listener");
		}
		_activationListeners.Add(listener);
		if (_preprocessor != null)
		{
			_preprocessor.Subscribe(listener);
		}
	}

	public void Unsubscribe([NotNull] IVoiceActivationListener listener)
	{
		if (listener == null)
		{
			throw new ArgumentNullException("listener", "Cannot unsubscribe with a null listener");
		}
		_activationListeners.Remove(listener);
		if (_preprocessor != null)
		{
			_preprocessor.Unsubscribe(listener);
		}
	}

	public void Subscribe([NotNull] IMicrophoneSubscriber listener)
	{
		if (listener == null)
		{
			throw new ArgumentNullException("listener", "Cannot subscribe with a null listener");
		}
		_audioListeners.Add(listener);
		if (_preprocessor != null)
		{
			_preprocessor.Subscribe(listener);
		}
	}

	public void Unsubscribe([NotNull] IMicrophoneSubscriber listener)
	{
		if (listener == null)
		{
			throw new ArgumentNullException("listener", "Cannot unsubscribe with a null listener");
		}
		_audioListeners.Remove(listener);
		if (_preprocessor != null)
		{
			_preprocessor.Unsubscribe(listener);
		}
	}

	internal void Pause()
	{
		StopTransmissionPipeline();
	}

	internal void Resume([CanBeNull] string reason = null)
	{
		RestartTransmissionPipeline(reason ?? "Editor resumed from pause");
	}

	public void ForceReset()
	{
		Log.Warn("Forcing capture pipeline reset");
		_pendingResetRequest = true;
		_cannotStartMic = false;
		_encounteredFatalException = false;
	}
}
