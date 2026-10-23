using Content.Shared._FinalStand.Perks;
using NUnit.Framework;

namespace Content.Tests.Shared._FinalStand;

[TestFixture]
public sealed class FSPerkMasteryTest
{
    [TestCase(0, FSMasteryTier.None, 500)]
    [TestCase(499, FSMasteryTier.None, 500)]
    [TestCase(500, FSMasteryTier.Bronze, 2500)]
    [TestCase(2499, FSMasteryTier.Bronze, 2500)]
    [TestCase(2500, FSMasteryTier.Silver, 10000)]
    [TestCase(10000, FSMasteryTier.Gold, null)]
    [TestCase(99999, FSMasteryTier.Gold, null)]
    public void TiersFollowTheThresholds(int kills, FSMasteryTier tier, int? next)
    {
        Assert.Multiple(() =>
        {
            Assert.That(FSPerkMastery.Tier(kills), Is.EqualTo(tier));
            Assert.That(FSPerkMastery.NextThreshold(kills), Is.EqualTo(next));
        });
    }
}
