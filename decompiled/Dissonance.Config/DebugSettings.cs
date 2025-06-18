using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace Dissonance.Config;

public class DebugSettings : ScriptableObject
{
	private const string SettingsFileResourceName = "DebugSettings";

	public static readonly string SettingsFilePath = Path.Combine("Assets/Plugins/Dissonance/Resources", "DebugSettings.asset");

	private const LogLevel DefaultLevel = LogLevel.Info;

	[SerializeField]
	private List<LogLevel> _levels;

	public bool EnableRecordingDiagnostics;

	public bool RecordMicrophoneRawAudio;

	public bool RecordPreprocessorOutput;

	public bool EnablePlaybackDiagnostics;

	public bool RecordDecodedAudio;

	public bool RecordFinalAudio;

	public bool EnableNetworkSimulation;

	public float PacketLoss;

	private static DebugSettings _instance;

	[NotNull]
	public static DebugSettings Instance
	{
		get
		{
			if ((Object)(object)_instance == (Object)null)
			{
				_instance = Load();
			}
			return _instance;
		}
	}

	public DebugSettings()
	{
		int num = ((LogCategory[])Enum.GetValues(typeof(LogCategory))).Select((LogCategory c) => (int)c).Max();
		_levels = new List<LogLevel>(num + 1);
	}

	public LogLevel GetLevel(int category)
	{
		if (_levels.Count > category)
		{
			return _levels[category];
		}
		return LogLevel.Info;
	}

	public void SetLevel(int category, LogLevel level)
	{
		if (_levels.Count <= category)
		{
			for (int i = _levels.Count; i <= category; i++)
			{
				_levels.Add(LogLevel.Info);
			}
		}
		_levels[category] = level;
	}

	private static DebugSettings Load()
	{
		DebugSettings debugSettings = Resources.Load<DebugSettings>("DebugSettings");
		if ((Object)(object)debugSettings == (Object)null)
		{
			debugSettings = ScriptableObject.CreateInstance<DebugSettings>();
		}
		return debugSettings;
	}

	public static void Preload()
	{
		if ((Object)(object)_instance == (Object)null)
		{
			_instance = Load();
		}
	}
}
