using System;
using System.IO;
using Dissonance.Threading;
using JetBrains.Annotations;
using NAudio.Wave;

namespace Dissonance.Audio;

internal class AudioFileWriter : IDisposable
{
	private static readonly Log Log = Logs.Create(LogCategory.Core, typeof(AudioFileWriter).Name);

	private readonly LockedValue<WaveFileWriter> _lock;

	private readonly bool _error;

	public AudioFileWriter(string filename, [NotNull] WaveFormat format)
	{
		if (filename == null)
		{
			throw new ArgumentNullException("filename");
		}
		if (format == null)
		{
			throw new ArgumentNullException("format");
		}
		if (string.IsNullOrEmpty(Path.GetExtension(filename)))
		{
			filename += ".wav";
		}
		try
		{
			string directoryName = Path.GetDirectoryName(filename);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			_lock = new LockedValue<WaveFileWriter>(new WaveFileWriter(File.Open(filename, FileMode.CreateNew), format));
		}
		catch (Exception arg)
		{
			Log.Error($"Attempting to create `AudioFileWriter` failed (audio logging will be disabled). This is often caused by a lack of permission to write to the specified directory.\nException: {arg}");
			_error = true;
		}
	}

	public void Dispose()
	{
		if (_error)
		{
			return;
		}
		using LockedValue<WaveFileWriter>.Unlocker unlocker = _lock.Lock();
		unlocker.Value?.Dispose();
		unlocker.Value = null;
	}

	public void Flush()
	{
		if (_error)
		{
			return;
		}
		using LockedValue<WaveFileWriter>.Unlocker unlocker = _lock.Lock();
		unlocker.Value?.Flush();
	}

	public void WriteSamples(ArraySegment<float> samples)
	{
		if (_error)
		{
			return;
		}
		using LockedValue<WaveFileWriter>.Unlocker unlocker = _lock.Lock();
		unlocker.Value?.WriteSamples(samples.Array, samples.Offset, samples.Count);
	}
}
