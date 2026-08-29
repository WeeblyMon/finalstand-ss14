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
            new FSLeaderboardEntry("Charlie", 5, 1, 1200, 10, 3, 1500, 1500),
            new FSLeaderboardEntry("Alice", 6, 0, 1500, 12, 2, 2200, 2200),
            new FSLeaderboardEntry("Bob", 6, 0, 1500, 12, 2, 1800, 1800),
        };

        var sorted = FSLeaderboardEntry.Sort(entries).ToArray();

        Assert.That(sorted[0].Name, Is.EqualTo("Alice"));
        Assert.That(sorted[1].Name, Is.EqualTo("Bob"));
        Assert.That(sorted[2].Name, Is.EqualTo("Charlie"));
    }
}
