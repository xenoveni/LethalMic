using JetBrains.Annotations;

namespace Dissonance;

public struct RoomMembership
{
	private readonly string _name;

	private readonly ushort _roomId;

	internal int Count;

	[NotNull]
	public string RoomName => _name;

	public ushort RoomId => _roomId;

	internal RoomMembership([NotNull] string name, int count)
	{
		_name = name;
		_roomId = name.ToRoomId();
		Count = count;
	}
}
