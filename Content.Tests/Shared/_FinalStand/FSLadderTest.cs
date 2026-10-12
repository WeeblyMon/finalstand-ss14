using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using NUnit.Framework;

namespace Content.Tests.Shared._FinalStand;

[TestFixture]
public sealed class FSLadderTest
{
    private static FSRunRecord Run(int waves, bool victory, int seconds, int day = 1)
        => new("A", FSDifficulty.Normal, FSPlayerBracket.Crew, waves, victory, seconds, 0, new DateTime(2026, 1, day));

    [TestCase(0, FSPlayerBracket.Solo)]
    [TestCase(1, FSPlayerBracket.Solo)]
    [TestCase(2, FSPlayerBracket.Squad)]
    [TestCase(4, FSPlayerBracket.Squad)]
    [TestCase(5, FSPlayerBracket.Crew)]
    [TestCase(30, FSPlayerBracket.Crew)]
    public void BracketsSplitByTeamSize(int players, FSPlayerBracket expected)
    {
        Assert.That(FSLadder.BracketFor(players), Is.EqualTo(expected));
    }

    [Test]
    public void VictoryThenWavesThenSpeedThenAge()
    {
        var oldFastWin = Run(30, true, 5000, day: 1);
        var newFastWin = Run(30, true, 5000, day: 2);
        var slowWin = Run(30, true, 9000);
        var deepLoss = Run(29, false, 1000);
        var shallowLoss = Run(10, false, 100);

        var shuffled = new[] { shallowLoss, slowWin, newFastWin, deepLoss, oldFastWin };
        var ranked = shuffled.OrderBy(r => r, Comparer<FSRunRecord>.Create(FSLadder.Compare)).ToArray();

        Assert.That(ranked, Is.EqualTo(new[] { oldFastWin, newFastWin, slowWin, deepLoss, shallowLoss }),
            "a tie goes to whoever set it first, so a record can't be taken by matching it");
    }

    [Test]
    public void OnlyAStrictImprovementTakesTheRecord()
    {
        var holder = Run(20, false, 3000, day: 1);
        Assert.Multiple(() =>
        {
            Assert.That(FSLadder.Beats(Run(5, false, 10), null), Is.True, "an empty ladder is taken by any run");
            Assert.That(FSLadder.Beats(Run(21, false, 9999), holder), Is.True);
            Assert.That(FSLadder.Beats(Run(20, false, 2999), holder), Is.True);
            Assert.That(FSLadder.Beats(Run(20, false, 3000, day: 2), holder), Is.False);
            Assert.That(FSLadder.Beats(Run(19, false, 1), holder), Is.False);
        });
    }
}
