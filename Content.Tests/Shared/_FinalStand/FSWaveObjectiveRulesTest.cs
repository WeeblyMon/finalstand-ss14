using System;
using System.Collections.Generic;
using Content.Shared._FinalStand.GameTicking;
using NUnit.Framework;

namespace Content.Tests.Shared._FinalStand;

[TestFixture]
[TestOf(typeof(FSWaveObjectiveRules))]
public sealed class FSWaveObjectiveRulesTest
{
    [Test]
    public void BossAndDarkWavesNeverGetObjectives()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FSWaveObjectiveRules.Eligible(3, 6, false, false), Is.True);
            Assert.That(FSWaveObjectiveRules.Eligible(3, 2, false, false), Is.False);
            Assert.That(FSWaveObjectiveRules.Eligible(3, 10, true, false), Is.False);
            Assert.That(FSWaveObjectiveRules.Eligible(3, 8, false, true), Is.False);
        });
    }

    [Test]
    public void KillQuotasGrowWithTheCrew()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FSWaveObjectiveRules.Target(25, 8, 1), Is.EqualTo(25));
            Assert.That(FSWaveObjectiveRules.Target(25, 8, 0), Is.EqualTo(25));
            Assert.That(FSWaveObjectiveRules.Target(25, 8, 4), Is.EqualTo(49));
        });
    }

    [Test]
    public void RewardsScaleWithWaveAndRewardMultiplier()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FSWaveObjectiveRules.Reward(300, 30, 10, 1f), Is.EqualTo(600));
            Assert.That(FSWaveObjectiveRules.Reward(300, 30, 10, 1.5f), Is.EqualTo(900));
            Assert.That(FSWaveObjectiveRules.Reward(300, 30, 10, -1f), Is.Zero);
        });
    }

    [Test]
    public void PickingFollowsWeights()
    {
        var pool = new List<string> { "common", "rare" };
        Func<string, float> weight = s => s == "common" ? 3f : 1f;
        Assert.Multiple(() =>
        {
            Assert.That(FSWaveObjectiveRules.Pick(pool, weight, 0f), Is.EqualTo("common"));
            Assert.That(FSWaveObjectiveRules.Pick(pool, weight, 0.74f), Is.EqualTo("common"));
            Assert.That(FSWaveObjectiveRules.Pick(pool, weight, 0.76f), Is.EqualTo("rare"));
            Assert.That(FSWaveObjectiveRules.Pick(pool, weight, 1f), Is.EqualTo("rare"));
            Assert.That(FSWaveObjectiveRules.Pick(pool, _ => 0f, 0.5f), Is.Null);
            Assert.That(FSWaveObjectiveRules.Pick(new List<string>(), weight, 0.5f), Is.Null);
        });
    }
}
