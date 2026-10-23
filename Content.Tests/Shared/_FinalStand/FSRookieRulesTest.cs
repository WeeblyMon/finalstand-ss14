using System;
using Content.Shared._FinalStand.Onboarding;
using NUnit.Framework;

namespace Content.Tests.Shared._FinalStand;

[TestFixture]
public sealed class FSRookieRulesTest
{
    [Test]
    public void TheThresholdIsExclusive()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FSRookieRules.IsRookie(TimeSpan.Zero), Is.True);
            Assert.That(FSRookieRules.IsRookie(FSRookieRules.RookieThreshold - TimeSpan.FromSeconds(1)), Is.True);
            Assert.That(FSRookieRules.IsRookie(FSRookieRules.RookieThreshold), Is.False,
                "at exactly ten hours the player has graduated");
        });
    }

    [Test]
    public void TheRookieRewardsMatchTheBlueprint()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FSRookieRules.StarterPerkPoints, Is.EqualTo(5));
            Assert.That(FSRookieRules.FirstRoundPerkPoints, Is.EqualTo(5));
            Assert.That(FSRookieRules.GraduationPerkPoints, Is.EqualTo(3));
            Assert.That(FSRookieRules.CatchUpXpMultiplier, Is.EqualTo(2f));
        });
    }
}
