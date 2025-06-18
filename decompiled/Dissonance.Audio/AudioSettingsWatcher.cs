using UnityEngine;

namespace Dissonance.Audio;

internal class AudioSettingsWatcher
{
	private static readonly AudioSettingsWatcher Singleton = new AudioSettingsWatcher();

	private readonly object _lock = new object();

	private bool _started;

	private AudioConfiguration _config;

	public static AudioSettingsWatcher Instance => Singleton;

	public AudioConfiguration Configuration
	{
		get
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			lock (_lock)
			{
				return _config;
			}
		}
	}

	internal void Start()
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Expected O, but got Unknown
		if (_started)
		{
			return;
		}
		lock (_lock)
		{
			if (!_started)
			{
				AudioSettings.OnAudioConfigurationChanged += new AudioConfigurationChangeHandler(OnAudioConfigChanged);
				OnAudioConfigChanged(devicewaschanged: true);
			}
			_started = true;
		}
	}

	private void OnAudioConfigChanged(bool devicewaschanged)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		lock (_lock)
		{
			_config = AudioSettings.GetConfiguration();
		}
	}
}
