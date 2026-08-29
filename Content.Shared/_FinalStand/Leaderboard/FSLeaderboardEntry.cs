using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Leaderboard;

// A record so the server can cheaply tell whether a snapshot actually changed before sending it.
[Serializable, NetSerializable]
public sealed record FSLeaderboardEntry(
    string Name,
    int Kills,
    int Assists,
    int Xp,
    int Level,
    int Prestige,
    int Credits,
    int Score)
{
    public static FSLeaderboardEntry[] Sort(IEnumerable<FSLeaderboardEntry> entries)
    {
        return entries
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
