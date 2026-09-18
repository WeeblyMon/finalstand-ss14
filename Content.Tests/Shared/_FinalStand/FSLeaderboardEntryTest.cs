using System.Linq;
using Content.Shared._FinalStand.Leaderboard;
using NUnit.Framework;

namespace Content.Tests.Shared._FinalStand;

[TestFixture]
public sealed class FSLeaderboardEntryTest
{
    [Test]
    public void SortsByScoreAndThenName()
    {
        var entries = new[]
        {
            new FSLeaderboardEntry("Charlie", Kills: 5, Assists: 1, Healing: 0,
                Xp: 1200, Level: 10, Prestige: 3, Credits: 1500, Score: 1500),
            new FSLeaderboardEntry("Alice", Kills: 6, Assists: 0, Healing: 400,
                Xp: 1500, Level: 12, Prestige: 2, Credits: 2200, Score: 2200),
            new FSLeaderboardEntry("Bob", Kills: 6, Assists: 0, Healing: 0,
                Xp: 1500, Level: 12, Prestige: 2, Credits: 1800, Score: 1800),
        };

        var sorted = FSLeaderboardEntry.Sort(entries).ToArray();

        Assert.That(sorted[0].Name, Is.EqualTo("Alice"));
        Assert.That(sorted[1].Name, Is.EqualTo("Bob"));
        Assert.That(sorted[2].Name, Is.EqualTo("Charlie"));
    }
}
