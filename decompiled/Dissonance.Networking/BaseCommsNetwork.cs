using System;
using System.Collections.Generic;
using Dissonance.Networking.Client;
using Dissonance.Networking.Server;
using Dissonance.Networking.Server.Admin;
using JetBrains.Annotations;
using UnityEngine;

namespace Dissonance.Networking;

public abstract class BaseCommsNetwork<TServer, TClient, TPeer, TClientParam, TServerParam> : MonoBehaviour, ICommsNetwork, ICommsNetworkState where TServer : BaseServer<TServer, TClient, TPeer> where TClient : BaseClient<TServer, TClient, TPeer> where TPeer : struct, IEquatable<TPeer>
{
	private interface IState
	{
		ConnectionStatus Status { get; }

		void Enter();

		void Update();

		void Exit();
	}

	private class Idle : IState
	{
		private readonly BaseCommsNetwork<TServer, TClient, TPeer, TClientParam, TServerParam> _net;

		public ConnectionStatus Status => ConnectionStatus.Disconnected;

		public Idle(BaseCommsNetwork<TServer, TClient, TPeer, TClientParam, TServerParam> net)
		{
			_net = net;
		}

		public void Enter()
		{
			_net.Mode = NetworkMode.None;
		}

		public void Update()
		{
		}

		public void Exit()
		{
		}
	}

	private class Session : IState
	{
		[CanBeNull]
		private readonly TClientParam _clientParameter;

		[CanBeNull]
		private readonly TServerParam _serverParameter;

		private readonly NetworkMode _mode;

		private readonly BaseCommsNetwork<TServer, TClient, TPeer, TClientParam, TServerParam> _net;

		private float _reconnectionAttemptInterval;

		private DateTime _lastReconnectionAttempt;

		public ConnectionStatus Status
		{
			get
			{
				bool num = !_mode.IsServerEnabled() || _net.Server != null;
				bool flag = !_mode.IsClientEnabled() || (_net.Client != null && _net.Client.IsConnected);
				if (num && flag)
				{
					return ConnectionStatus.Connected;
				}
				return ConnectionStatus.Degraded;
			}
		}

		public Session([NotNull] BaseCommsNetwork<TServer, TClient, TPeer, TClientParam, TServerParam> net, NetworkMode mode, [CanBeNull] TServerParam serverParameter, [CanBeNull] TClientParam clientParameter)
		{
			_net = net;
			_clientParameter = clientParameter;
			_serverParameter = serverParameter;
			_mode = mode;
		}

		public void Enter()
		{
			_net.Mode = _mode;
			if (_mode.IsServerEnabled())
			{
				StartServer();
			}
			if (_mode.IsClientEnabled())
			{
				StartClient();
			}
		}

		public void Update()
		{
			if (_mode.IsServerEnabled() && _net.Server.Update() == ServerState.Error)
			{
				_net.Log.Warn("Server update encountered an error - restarting server");
				_net.StopServer();
				StartServer();
			}
			if (!_mode.IsClientEnabled())
			{
				return;
			}
			if (_net.Client != null)
			{
				if (_net.Client.Update() == ClientStatus.Error)
				{
					_net.Log.Warn("Client update encountered an error - shutting down client");
					_net.StopClient();
				}
				else
				{
					_reconnectionAttemptInterval = Math.Max(0f, _reconnectionAttemptInterval - Time.unscaledDeltaTime);
				}
			}
			if (_net.Client == null && ShouldAttemptReconnect())
			{
				_net.Log.Info("Attempting to restart client");
				StartClient();
				_reconnectionAttemptInterval = Math.Min(3f, _reconnectionAttemptInterval + 0.5f);
			}
		}

		public void Exit()
		{
			if (_net.Client != null)
			{
				_net.StopClient();
			}
			if (_net.Server != null)
			{
				_net.StopServer();
			}
		}

		private void StartServer()
		{
			_net.StartServer(_serverParameter);
		}

		private void StartClient()
		{
			_net.StartClient(_clientParameter);
			_lastReconnectionAttempt = DateTime.UtcNow;
		}

		private bool ShouldAttemptReconnect()
		{
			return (DateTime.UtcNow - _lastReconnectionAttempt).TotalSeconds >= (double)_reconnectionAttemptInterval;
		}
	}

	private readonly Queue<IState> _nextStates;

	private IState _state;

	private NetworkMode _mode;

	protected readonly Log Log;

	protected TServer Server { get; private set; }

	protected TClient Client { get; private set; }

	public string PlayerName { get; private set; }

	public Rooms Rooms { get; private set; }

	public PlayerChannels PlayerChannels { get; private set; }

	public RoomChannels RoomChannels { get; private set; }

	public CodecSettings CodecSettings { get; private set; }

	public bool IsInitialized { get; private set; }

	public ConnectionStatus Status => _state.Status;

	public NetworkMode Mode
	{
		get
		{
			return _mode;
		}
		private set
		{
			if (_mode != value)
			{
				_mode = value;
				OnModeChanged(value);
			}
		}
	}

	[CanBeNull]
	public IServerAdmin ServerAdmin => Server?.ServerAdmin;

	public event Action<NetworkMode> ModeChanged;

	public event Action<string, CodecSettings> PlayerJoined;

	public event Action<string> PlayerLeft;

	public event Action<VoicePacket> VoicePacketReceived;

	public event Action<TextMessage> TextPacketReceived;

	public event Action<string> PlayerStartedSpeaking;

	public event Action<string> PlayerStoppedSpeaking;

	public event Action<RoomEvent> PlayerEnteredRoom;

	public event Action<RoomEvent> PlayerExitedRoom;

	protected BaseCommsNetwork()
	{
		Log = Logs.Create(LogCategory.Network, ((object)this).GetType().Name);
		_nextStates = new Queue<IState>();
		_mode = NetworkMode.None;
		_state = new Idle(this);
	}

	[NotNull]
	protected abstract TServer CreateServer([CanBeNull] TServerParam connectionParameters);

	[NotNull]
	protected abstract TClient CreateClient([CanBeNull] TClientParam connectionParameters);

	protected virtual void Initialize()
	{
	}

	void ICommsNetwork.Initialize([NotNull] string playerName, [NotNull] Rooms rooms, [NotNull] PlayerChannels playerChannels, [NotNull] RoomChannels roomChannels, CodecSettings codecSettings)
	{
		if (playerName == null)
		{
			throw new ArgumentNullException("playerName");
		}
		if (rooms == null)
		{
			throw new ArgumentNullException("rooms");
		}
		if (playerChannels == null)
		{
			throw new ArgumentNullException("playerChannels");
		}
		if (roomChannels == null)
		{
			throw new ArgumentNullException("roomChannels");
		}
		PlayerName = playerName;
		Rooms = rooms;
		PlayerChannels = playerChannels;
		RoomChannels = roomChannels;
		CodecSettings = codecSettings;
		Initialize();
		IsInitialized = true;
	}

	protected virtual void Update()
	{
		if (IsInitialized)
		{
			LoadState();
			_state.Update();
		}
	}

	private void LoadState()
	{
		while (_nextStates.Count > 0)
		{
			ChangeState(_nextStates.Dequeue());
		}
	}

	protected virtual void OnDisable()
	{
		Stop();
		LoadState();
	}

	public void Stop()
	{
		_nextStates.Enqueue(new Idle(this));
	}

	protected void RunAsHost(TServerParam serverParameters, TClientParam clientParameters)
	{
		_nextStates.Enqueue(new Session(this, NetworkMode.Host, serverParameters, clientParameters));
	}

	protected void RunAsClient(TClientParam clientParameters)
	{
		_nextStates.Enqueue(new Session(this, NetworkMode.Client, default(TServerParam), clientParameters));
	}

	protected void RunAsDedicatedServer(TServerParam serverParameters)
	{
		_nextStates.Enqueue(new Session(this, NetworkMode.DedicatedServer, serverParameters, default(TClientParam)));
	}

	private void ChangeState(IState newState)
	{
		_state.Exit();
		_state = newState;
		_state.Enter();
	}

	private void StartServer([CanBeNull] TServerParam connectParams)
	{
		if (Server != null)
		{
			throw Log.CreatePossibleBugException("Attempted to start the network server while the server is already running", "680CB0B1-1F2C-4EB2-A249-3EDD513354B9");
		}
		Server = CreateServer(connectParams);
		Server.Connect();
	}

	private void StopServer()
	{
		if (Server == null)
		{
			throw Log.CreatePossibleBugException("Attempted to stop the network server while the server is not running", "BCA52BAC-DE86-4037-9C7B-508D1798E50B");
		}
		try
		{
			Server.Disconnect();
		}
		catch (Exception ex)
		{
			Log.Error("Encountered error shutting down server: '{0}'", ex.Message);
		}
		Server = null;
	}

	private void StartClient([CanBeNull] TClientParam connectParams)
	{
		if (Client != null)
		{
			throw Log.CreatePossibleBugException("Attempted to start client while the client is already running", "0AEB8FC5-025F-46F5-969A-B792D2E84626");
		}
		Client = CreateClient(connectParams);
		Client.PlayerJoined += OnPlayerJoined;
		Client.PlayerLeft += OnPlayerLeft;
		Client.PlayerEnteredRoom += OnPlayerEnteredRoom;
		Client.PlayerExitedRoom += OnPlayerExitedRoom;
		Client.VoicePacketReceived += OnVoicePacketReceived;
		Client.TextMessageReceived += OnTextPacketReceived;
		Client.PlayerStartedSpeaking += OnPlayerStartedSpeaking;
		Client.PlayerStoppedSpeaking += OnPlayerStoppedSpeaking;
		Client.Connect();
	}

	private void StopClient()
	{
		if (Client == null)
		{
			throw Log.CreatePossibleBugException("Attempted to stop the client while the client is not running", "F44A101A-6EF3-4668-9E29-2447B0137921");
		}
		try
		{
			Client.Disconnect();
		}
		catch (Exception ex)
		{
			Log.Error("Encountered error shutting down client: '{0}'", ex.Message);
		}
		Client.PlayerJoined -= OnPlayerJoined;
		Client.PlayerLeft -= OnPlayerLeft;
		Client.VoicePacketReceived -= OnVoicePacketReceived;
		Client.TextMessageReceived -= OnTextPacketReceived;
		Client.PlayerStartedSpeaking -= OnPlayerStartedSpeaking;
		Client.PlayerStoppedSpeaking -= OnPlayerStoppedSpeaking;
		Client = null;
	}

	public void SendVoice(ArraySegment<byte> data)
	{
		if (Client != null)
		{
			Client.SendVoiceData(data);
		}
	}

	public void SendText(string data, ChannelType recipientType, string recipientId)
	{
		if (Client != null)
		{
			Client.SendTextData(data, recipientType, recipientId);
		}
	}

	private void OnPlayerJoined(string obj, CodecSettings codecSettings)
	{
		this.PlayerJoined?.Invoke(obj, codecSettings);
	}

	private void OnPlayerLeft(string obj)
	{
		this.PlayerLeft?.Invoke(obj);
	}

	private void OnPlayerEnteredRoom(RoomEvent evt)
	{
		this.PlayerEnteredRoom?.Invoke(evt);
	}

	private void OnPlayerExitedRoom(RoomEvent evt)
	{
		this.PlayerExitedRoom?.Invoke(evt);
	}

	private void OnVoicePacketReceived(VoicePacket obj)
	{
		this.VoicePacketReceived?.Invoke(obj);
	}

	private void OnTextPacketReceived(TextMessage obj)
	{
		this.TextPacketReceived?.Invoke(obj);
	}

	private void OnPlayerStartedSpeaking(string obj)
	{
		this.PlayerStartedSpeaking?.Invoke(obj);
	}

	private void OnPlayerStoppedSpeaking(string obj)
	{
		this.PlayerStoppedSpeaking?.Invoke(obj);
	}

	private void OnModeChanged(NetworkMode obj)
	{
		this.ModeChanged?.Invoke(obj);
	}

	public void OnInspectorGui()
	{
	}
}
