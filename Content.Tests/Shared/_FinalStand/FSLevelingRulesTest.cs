using Content.Shared._FinalStand.Leveling;
using NUnit.Framework;

namespace Content.Tests.Shared._FinalStand;

[TestFixture]
public sealed class FSLevelingRulesTest
{
    [TestCase(2, 1)]
    [TestCase(4, 1)]
    [TestCase(5, 5)]
    [TestCase(10, 5)]
    [TestCase(11, 1)]
    public void EveryFifthLevelPaysFive(int level, int points)
    {
        Assert.That(FSLevelingRules.PerkPointsForReaching(level), Is.EqualTo(points));
    }
}
