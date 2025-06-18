using System.Collections.ObjectModel;

namespace Dissonance.Audio.Playback;

internal class PriorityManager : IPriorityManager
{
	private static readonly Log Log = Logs.Create(LogCategory.Playback, typeof(PriorityManager).Name);

	private readonly PlayerCollection _players;

	public ChannelPriority TopPriority { get; private set; }

	public PriorityManager(PlayerCollection players)
	{
		_players = players;
		TopPriority = ChannelPriority.None;
	}

	public void Update()
	{
		ChannelPriority channelPriority = ChannelPriority.None;
		ReadOnlyCollection<VoicePlayerState> readOnlyCollection = _players.Readonly;
		for (int i = 0; i < readOnlyCollection.Count; i++)
		{
			VoicePlayerState voicePlayerState = readOnlyCollection[i];
			ChannelPriority? speakerPriority = voicePlayerState.SpeakerPriority;
			if (speakerPriority.HasValue && speakerPriority > channelPriority)
			{
				channelPriority = speakerPriority.Value;
				_ = voicePlayerState.Name;
			}
		}
		if (TopPriority != channelPriority)
		{
			TopPriority = channelPriority;
		}
	}
}
