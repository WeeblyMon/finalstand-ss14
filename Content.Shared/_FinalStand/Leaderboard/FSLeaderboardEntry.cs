using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Leaderboard;

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
