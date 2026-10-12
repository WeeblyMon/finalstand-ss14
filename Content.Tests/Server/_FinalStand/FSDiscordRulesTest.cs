using System;
using Content.Server._FinalStand.Discord;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using NUnit.Framework;

namespace Content.Tests.Server._FinalStand;

[TestFixture]
public sealed class FSDiscordRulesTest
{
    private static FSRunRecord Run(int waves, bool victory = false, int seconds = 3000)
        => new("A", FSDifficulty.Normal, FSPlayerBracket.Crew, waves, victory, seconds, 0, DateTime.UnixEpoch);

    [Test]
    public void NearlyBeatenMeansWithinTenPercentWithoutTakingTheRecord()
    {
        var holder = Run(20);
        Assert.Multiple(() =>
        {
            Assert.That(FSDiscordMessages.IsNearlyBeaten(Run(18), holder), Is.True);
            Assert.That(FSDiscordMessages.IsNearlyBeaten(Run(19), holder), Is.True);
            Assert.That(FSDiscordMessages.IsNearlyBeaten(Run(17), holder), Is.False, "more than 10% short is not close");
            Assert.That(FSDiscordMessages.IsNearlyBeaten(Run(21), holder), Is.False, "that's a new record, not a near miss");
            Assert.That(FSDiscordMessages.IsNearlyBeaten(Run(20, seconds: 4000), holder), Is.False);
            Assert.That(FSDiscordMessages.IsNearlyBeaten(Run(18), null), Is.False, "nothing to be close to");
        });
    }

    [Test]
    public void RareTitlesAreHeldByAtMostFivePercent()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FSDiscordMessages.IsRareTitle(1, 10), Is.True, "with few players the first holder is always rare");
            Assert.That(FSDiscordMessages.IsRareTitle(2, 10), Is.False);
            Assert.That(FSDiscordMessages.IsRareTitle(5, 100), Is.True);
            Assert.That(FSDiscordMessages.IsRareTitle(6, 100), Is.False);
            Assert.That(FSDiscordMessages.IsRareTitle(1, 0), Is.False);
        });
    }
}
