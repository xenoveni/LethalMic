using System;
using Dissonance.Audio.Playback;
using Dissonance.Datastructures;
using JetBrains.Annotations;
using UnityEngine;

namespace Dissonance;

internal class PlaybackPool
{
	private readonly Pool<VoicePlayback> _pool;

	[NotNull]
	private readonly IPriorityManager _priority;

	[NotNull]
	private readonly IVolumeProvider _volume;

	private GameObject _prefab;

	private Transform _parent;

	public PlaybackPool([NotNull] IPriorityManager priority, [NotNull] IVolumeProvider volume)
	{
		if (priority == null)
		{
			throw new ArgumentNullException("priority");
		}
		if (volume == null)
		{
			throw new ArgumentNullException("volume");
		}
		_priority = priority;
		_volume = volume;
		_pool = new Pool<VoicePlayback>(10, CreatePlayback);
	}

	public void Start([NotNull] GameObject playbackPrefab, [NotNull] Transform transform)
	{
		if ((Object)(object)playbackPrefab == (Object)null)
		{
			throw new ArgumentNullException("playbackPrefab");
		}
		if ((Object)(object)transform == (Object)null)
		{
			throw new ArgumentNullException("transform");
		}
		_prefab = playbackPrefab;
		_parent = transform;
	}

	[NotNull]
	private VoicePlayback CreatePlayback()
	{
		_prefab.gameObject.SetActive(false);
		GameObject val = Object.Instantiate<GameObject>(_prefab.gameObject);
		val.transform.parent = _parent;
		AudioSource val2 = val.GetComponent<AudioSource>();
		if ((Object)(object)val2 == (Object)null)
		{
			val2 = val.AddComponent<AudioSource>();
			val2.rolloffMode = (AudioRolloffMode)1;
			val2.bypassReverbZones = true;
		}
		val2.loop = true;
		val2.pitch = 1f;
		val2.clip = null;
		val2.playOnAwake = false;
		val2.ignoreListenerPause = true;
		val2.Stop();
		if ((Object)(object)val.GetComponent<SamplePlaybackComponent>() == (Object)null)
		{
			val.AddComponent<SamplePlaybackComponent>();
		}
		VoicePlayback component = val.GetComponent<VoicePlayback>();
		component.PriorityManager = _priority;
		component.VolumeProvider = _volume;
		return component;
	}

	[NotNull]
	public VoicePlayback Get([NotNull] string playerId)
	{
		if (playerId == null)
		{
			throw new ArgumentNullException("playerId");
		}
		VoicePlayback voicePlayback = _pool.Get();
		((Object)((Component)voicePlayback).gameObject).name = $"Player {playerId} voice comms";
		voicePlayback.PlayerName = playerId;
		return voicePlayback;
	}

	public void Put([NotNull] VoicePlayback playback)
	{
		if ((Object)(object)playback == (Object)null)
		{
			throw new ArgumentNullException("playback");
		}
		((Component)playback).gameObject.SetActive(false);
		((Object)((Component)playback).gameObject).name = "Spare voice comms";
		playback.PlayerName = null;
		if (!_pool.Put(playback))
		{
			Object.Destroy((Object)(object)((Component)playback).gameObject);
		}
	}
}
