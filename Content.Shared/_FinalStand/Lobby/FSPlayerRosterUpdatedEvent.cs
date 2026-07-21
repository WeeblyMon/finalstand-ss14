using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Lobby;

[Serializable, NetSerializable]
public readonly record struct FSPlayerRosterEntry(string Name, bool IsAdmin);

[Serializable, NetSerializable]
public sealed class FSPlayerRosterRequestMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class FSPlayerRosterUpdatedEvent : EntityEventArgs
{
    public readonly List<FSPlayerRosterEntry> Players;
    public readonly int MaxPlayers;

    public FSPlayerRosterUpdatedEvent(List<FSPlayerRosterEntry> players, int maxPlayers)
    {
        Players = players;
        MaxPlayers = maxPlayers;
    }
}
