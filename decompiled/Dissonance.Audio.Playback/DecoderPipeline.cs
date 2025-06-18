using System;
using System.Collections.Generic;
using Dissonance.Audio.Codecs;
using Dissonance.Datastructures;
using Dissonance.Extensions;
using Dissonance.Networking;
using JetBrains.Annotations;
using NAudio.Wave;

namespace Dissonance.Audio.Playback;

internal class DecoderPipeline : IDecoderPipeline, IVolumeProvider, IRemoteChannelProvider, IRateProvider
{
	private static readonly Log Log = Logs.Create(LogCategory.Playback, typeof(DecoderPipeline).Name);

	private readonly Action<DecoderPipeline> _completionHandler;

	private readonly TransferBuffer<VoicePacket> _inputBuffer;

	private readonly ConcurrentPool<byte[]> _bytePool;

	private readonly ConcurrentPool<List<RemoteChannel>> _channelListPool;

	private readonly BufferedDecoder _source;

	private readonly SynchronizerSampleSource _synchronizer;

	private readonly ISampleSource _output;

	private volatile bool _prepared;

	private volatile bool _complete;

	private bool _sourceClosed;

	private readonly TimeSpan _frameDuration;

	private DateTime? _firstFrameArrival;

	private uint _firstFrameSeq;

	private readonly string _id;

	public int BufferCount => _source.BufferCount + _inputBuffer.EstimatedUnreadCount;

	public TimeSpan BufferTime => TimeSpan.FromTicks(BufferCount * _frameDuration.Ticks);

	public float PacketLoss => _source.PacketLoss;

	public PlaybackOptions PlaybackOptions => _source.LatestPlaybackOptions;

	public WaveFormat OutputFormat => _output.WaveFormat;

	public TimeSpan InputFrameTime => _frameDuration;

	float IRateProvider.PlaybackRate => _synchronizer.PlaybackRate;

	public SyncState SyncState => _synchronizer.State;

	public string ID => _id;

	public IVolumeProvider VolumeProvider { get; set; }

	float IVolumeProvider.TargetVolume => ((VolumeProvider == null) ? 1f : VolumeProvider.TargetVolume) * PlaybackOptions.AmplitudeMultiplier;

	public DecoderPipeline([NotNull] IVoiceDecoder decoder, uint inputFrameSize, [NotNull] Action<DecoderPipeline> completionHandler, string id, bool softClip = true)
	{
		if (decoder == null)
		{
			throw new ArgumentNullException("decoder");
		}
		if (completionHandler == null)
		{
			throw new ArgumentNullException("completionHandler");
		}
		_id = id;
		_completionHandler = completionHandler;
		_inputBuffer = new TransferBuffer<VoicePacket>(32);
		_bytePool = new ConcurrentPool<byte[]>(12, () => new byte[inputFrameSize * decoder.Format.Channels * 4]);
		_channelListPool = new ConcurrentPool<List<RemoteChannel>>(12, () => new List<RemoteChannel>());
		_frameDuration = TimeSpan.FromSeconds((double)inputFrameSize / (double)decoder.Format.SampleRate);
		_firstFrameArrival = null;
		_firstFrameSeq = 0u;
		_synchronizer = new SynchronizerSampleSource(new FrameToSampleConverter(new VolumeRampedFrameSource(_source = new BufferedDecoder(decoder, inputFrameSize, decoder.Format, RecycleFrame), this)), TimeSpan.FromSeconds(1.0));
		Resampler resampler = new Resampler(_synchronizer, this);
		ISampleSource output;
		if (!softClip)
		{
			ISampleSource sampleSource = resampler;
			output = sampleSource;
		}
		else
		{
			ISampleSource sampleSource = new SoftClipSampleSource(resampler);
			output = sampleSource;
		}
		_output = output;
	}

	private void RecycleFrame(VoicePacket packet)
	{
		_bytePool.Put(packet.EncodedAudioFrame.Array);
		if (packet.Channels != null)
		{
			packet.Channels.Clear();
			_channelListPool.Put(packet.Channels);
		}
	}

	public void Prepare(SessionContext context)
	{
		_output.Prepare(context);
		_prepared = true;
	}

	public void EnableDynamicSync()
	{
		_synchronizer.Enable();
	}

	public bool Read(ArraySegment<float> samples)
	{
		FlushTransferBuffer();
		bool num = _output.Read(samples);
		if (num)
		{
			_completionHandler(this);
		}
		return num;
	}

	public float Push(VoicePacket packet, DateTime now)
	{
		List<RemoteChannel> list = null;
		if (packet.Channels != null)
		{
			list = _channelListPool.Get();
			list.Clear();
			list.AddRange(packet.Channels);
		}
		ArraySegment<byte> encodedAudioFrame = packet.EncodedAudioFrame.CopyToSegment(_bytePool.Get());
		VoicePacket item = new VoicePacket(packet.SenderPlayerId, packet.PlaybackOptions.Priority, packet.PlaybackOptions.AmplitudeMultiplier, packet.PlaybackOptions.IsPositional, encodedAudioFrame, packet.SequenceNumber, list);
		if (!_inputBuffer.TryWrite(item))
		{
			Log.Warn("Failed to write an encoded audio packet into the input transfer buffer");
		}
		if (!_prepared)
		{
			FlushTransferBuffer();
		}
		if (!_firstFrameArrival.HasValue)
		{
			_firstFrameArrival = now;
			_firstFrameSeq = packet.SequenceNumber;
			return 0f;
		}
		DateTime dateTime = _firstFrameArrival.Value + TimeSpan.FromTicks(_frameDuration.Ticks * (packet.SequenceNumber - _firstFrameSeq));
		return (float)(now - dateTime).TotalSeconds;
	}

	public void Stop()
	{
		_complete = true;
	}

	public void Reset()
	{
		_output.Reset();
		_firstFrameArrival = null;
		_prepared = false;
		_complete = false;
		_sourceClosed = false;
		VolumeProvider = null;
	}

	public void FlushTransferBuffer()
	{
		VoicePacket item;
		while (_inputBuffer.Read(out item))
		{
			_source.Push(item);
		}
		if (_complete && !_sourceClosed)
		{
			_sourceClosed = true;
			_source.Stop();
		}
	}

	public void GetRemoteChannels(List<RemoteChannel> output)
	{
		output.Clear();
		_source.GetRemoteChannels(output);
	}
}
