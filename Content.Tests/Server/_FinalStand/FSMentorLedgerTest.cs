using System;
using Content.Server._FinalStand.Mentor;
using Content.Shared._FinalStand.Mentor;
using NUnit.Framework;

namespace Content.Tests.Server._FinalStand;

[TestFixture]
[TestOf(typeof(FSMentorLedger))]
public sealed class FSMentorLedgerTest
{
    private static readonly Guid Mentor = Guid.NewGuid();
    private static readonly Guid Rookie = Guid.NewGuid();
    private static readonly Guid OtherRookie = Guid.NewGuid();

    [Test]
    public void PointsAreCappedPerRookieEachRound()
    {
        var ledger = new FSMentorLedger();
        Assert.That(ledger.Grant(Mentor, Rookie, 8, 0), Is.EqualTo(8));
        Assert.That(ledger.Grant(Mentor, Rookie, 8, 8), Is.EqualTo(FSMentorLedger.PerRookieRoundCap - 8));
        Assert.That(ledger.Grant(Mentor, Rookie, 1, 10), Is.Zero);
        Assert.That(ledger.Grant(Mentor, OtherRookie, 3, 10), Is.EqualTo(3), "a different rookie has their own cap");
    }

    [Test]
    public void PointsAreCappedPerRound()
    {
        var ledger = new FSMentorLedger();
        var total = 0;
        for (var i = 0; i < 10; i++)
            total += ledger.Grant(Mentor, Guid.NewGuid(), FSMentorLedger.PerRookieRoundCap, total);

        Assert.That(total, Is.EqualTo(FSMentorLedger.RoundCap));
        Assert.That(ledger.EarnedThisRound(Mentor), Is.EqualTo(FSMentorLedger.RoundCap));
    }

    [Test]
    public void TheWeeklyCapHoldsAcrossRounds()
    {
        var ledger = new FSMentorLedger();
        Assert.That(ledger.Grant(Mentor, Rookie, 5, FSMentorLedger.WeekCap - 2), Is.EqualTo(2));
        Assert.That(ledger.Grant(Mentor, OtherRookie, 5, FSMentorLedger.WeekCap), Is.Zero);
    }

    [Test]
    public void HurtingARookieVoidsThePairUntilTheRoundResets()
    {
        var ledger = new FSMentorLedger();
        ledger.Void(Mentor, Rookie);
        Assert.Multiple(() =>
        {
            Assert.That(ledger.IsVoided(Mentor, Rookie), Is.True);
            Assert.That(ledger.Grant(Mentor, Rookie, 3, 0), Is.Zero);
            Assert.That(ledger.Grant(Mentor, OtherRookie, 3, 0), Is.EqualTo(3));
        });

        ledger.Reset();
        Assert.Multiple(() =>
        {
            Assert.That(ledger.IsVoided(Mentor, Rookie), Is.False);
            Assert.That(ledger.EarnedThisRound(Mentor), Is.Zero);
            Assert.That(ledger.Grant(Mentor, Rookie, 3, 0), Is.EqualTo(3));
        });
    }

    [Test]
    public void NobodyMentorsThemselvesOrEarnsNothing()
    {
        var ledger = new FSMentorLedger();
        Assert.That(ledger.Grant(Mentor, Mentor, 3, 0), Is.Zero);
        Assert.That(ledger.Grant(Mentor, Rookie, 0, 0), Is.Zero);
        Assert.That(ledger.Grant(Mentor, Rookie, -4, 0), Is.Zero);
    }

    [TestCase(24.9, 50, 0, false)]
    [TestCase(25, 14, 0, false)]
    [TestCase(25, 15, 0, true)]
    [TestCase(25, 1, 1, true)]
    [TestCase(300, 100, 3, true)]
    public void MentorsNeedPlaytimeAndLevel(double hours, int level, int prestige, bool expected)
    {
        Assert.That(FSMentorRules.IsEligible(TimeSpan.FromHours(hours), level, prestige), Is.EqualTo(expected));
    }

    [Test]
    public void HealingBanksTowardsWholePoints()
    {
        var bank = 0f;
        Assert.That(FSMentorRules.HealPoints(ref bank, 30f, 50f), Is.Zero);
        Assert.That(FSMentorRules.HealPoints(ref bank, 30f, 50f), Is.EqualTo(1));
        Assert.That(bank, Is.EqualTo(10f).Within(0.001f));
        Assert.That(FSMentorRules.HealPoints(ref bank, 140f, 50f), Is.EqualTo(3));
        Assert.That(FSMentorRules.HealPoints(ref bank, -500f, 50f), Is.Zero, "damage never drains the bank");
        Assert.That(bank, Is.EqualTo(0f).Within(0.001f));
    }
}
