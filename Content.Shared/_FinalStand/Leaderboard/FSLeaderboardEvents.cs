using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Leaderboard;

[Serializable, NetSerializable]
public sealed class FSLeaderboardUpdateEvent : EntityEventArgs
{
    public FSLeaderboardEntry[] Entries { get; }

    public FSLeaderboardUpdateEvent(FSLeaderboardEntry[] entries)
    {
        Entries = entries;
    }
}

[Serializable, NetSerializable]
public sealed class FSLeaderboardWatchEvent : EntityEventArgs
{
    public bool Watching { get; }

    public FSLeaderboardWatchEvent(bool watching)
    {
        Watching = watching;
    }
}
