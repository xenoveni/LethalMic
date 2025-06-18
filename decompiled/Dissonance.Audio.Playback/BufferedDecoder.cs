using System;
using System.Collections.Generic;
using Dissonance.Audio.Codecs;
using Dissonance.Config;
using Dissonance.Networking;
using Dissonance.Threading;
using JetBrains.Annotations;
using NAudio.Wave;

namespace Dissonance.Audio.Playback;

internal class BufferedDecoder : IFrameSource, IRemoteChannelProvider
{
	private readonly EncodedAudioBuffer _buffer;

	private readonly IVoiceDecoder _decoder;

	private readonly uint _frameSize;

	private readonly WaveFormat _waveFormat;

	private readonly Action<VoicePacket> _recycleFrame;

	private AudioFileWriter _diagnosticOutput;

	private readonly LockedValue<PlaybackOptions> _options = new LockedValue<PlaybackOptions>(new PlaybackOptions(isPositional: false, 1f, ChannelPriority.Default));

	private bool _receivedFirstPacket;

	private int _approxChannelCount;

	private readonly ReadonlyLockedValue<List<RemoteChannel>> _channels = new ReadonlyLockedValue<List<RemoteChannel>>(new List<RemoteChannel>());

	public int BufferCount => _buffer.Count;

	public uint SequenceNumber => _buffer.SequenceNumber;

	public float PacketLoss => _buffer.PacketLoss;

	public PlaybackOptions LatestPlaybackOptions
	{
		get
		{
			using LockedValue<PlaybackOptions>.Unlocker unlocker = _options.Lock();
			return unlocker.Value;
		}
	}

	public uint FrameSize => _frameSize;

	public WaveFormat WaveFormat => _waveFormat;

	public BufferedDecoder([NotNull] IVoiceDecoder decoder, uint frameSize, [NotNull] WaveFormat waveFormat, [NotNull] Action<VoicePacket> recycleFrame)
	{
		if (decoder == null)
		{
			throw new ArgumentNullException("decoder");
		}
		if (waveFormat == null)
		{
			throw new ArgumentNullException("waveFormat");
		}
		if (recycleFrame == null)
		{
			throw new ArgumentNullException("recycleFrame");
		}
		_decoder = decoder;
		_frameSize = frameSize;
		_waveFormat = waveFormat;
		_recycleFrame = recycleFrame;
		_buffer = new EncodedAudioBuffer(recycleFrame);
	}

	public void Prepare(SessionContext context)
	{
		if (DebugSettings.Instance.EnablePlaybackDiagnostics && DebugSettings.Instance.RecordDecodedAudio)
		{
			string filename = $"Dissonance_Diagnostics/Decoded_{context.PlayerName}_{context.Id}_{DateTime.UtcNow.ToFileTime()}";
			_diagnosticOutput = new AudioFileWriter(filename, _waveFormat);
		}
	}

	public bool Read(ArraySegment<float> frame)
	{
		VoicePacket? frame2;
		bool lostPacket;
		bool result = _buffer.Read(out frame2, out lostPacket);
		EncodedBuffer input = new EncodedBuffer(frame2.HasValue ? new ArraySegment<byte>?(frame2.Value.EncodedAudioFrame) : ((ArraySegment<byte>?)null), lostPacket || !frame2.HasValue);
		int num = _decoder.Decode(input, frame);
		if (!input.PacketLost && frame2.HasValue)
		{
			using (LockedValue<PlaybackOptions>.Unlocker unlocker = _options.Lock())
			{
				unlocker.Value = frame2.Value.PlaybackOptions;
			}
			ExtractChannels(frame2.Value);
			_recycleFrame(frame2.Value);
		}
		if (num != _frameSize)
		{
			throw new InvalidOperationException($"Decoding a frame of audio got {num} samples, but should have decoded {_frameSize} samples");
		}
		if (_diagnosticOutput != null)
		{
			_diagnosticOutput.WriteSamples(frame);
		}
		return result;
	}

	private void ExtractChannels(VoicePacket encoded)
	{
		if (encoded.Channels != null)
		{
			using (ReadonlyLockedValue<List<RemoteChannel>>.Unlocker unlocker = _channels.Lock())
			{
				_approxChannelCount = encoded.Channels.Count;
				unlocker.Value.Clear();
				unlocker.Value.AddRange(encoded.Channels);
			}
			_receivedFirstPacket = true;
		}
	}

	public void Reset()
	{
		_buffer.Reset();
		_decoder.Reset();
		_receivedFirstPacket = false;
		using (LockedValue<PlaybackOptions>.Unlocker unlocker = _options.Lock())
		{
			unlocker.Value = new PlaybackOptions(isPositional: false, 1f, ChannelPriority.Default);
		}
		using (ReadonlyLockedValue<List<RemoteChannel>>.Unlocker unlocker2 = _channels.Lock())
		{
			unlocker2.Value.Clear();
		}
		if (_diagnosticOutput != null)
		{
			_diagnosticOutput.Dispose();
			_diagnosticOutput = null;
		}
	}

	public void Push(VoicePacket frame)
	{
		if (!_receivedFirstPacket)
		{
			ExtractChannels(frame);
		}
		_buffer.Push(frame);
		_receivedFirstPacket = true;
	}

	public void Stop()
	{
		_buffer.Stop();
	}

	public void GetRemoteChannels(List<RemoteChannel> output)
	{
		output.Clear();
		if (output.Capacity < _approxChannelCount)
		{
			output.Capacity = _approxChannelCount;
		}
		using ReadonlyLockedValue<List<RemoteChannel>>.Unlocker unlocker = _channels.Lock();
		output.AddRange(unlocker.Value);
	}
}
