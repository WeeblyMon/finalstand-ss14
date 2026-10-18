using System;
using System.Linq;
using Content.Shared._FinalStand.GameTicking;
using NUnit.Framework;

namespace Content.Tests.Shared._FinalStand;

[TestFixture]
public sealed class FSDifficultyRulesTest
{
    private static readonly FSDifficulty[] Ordered = Enum.GetValues<FSDifficulty>().OrderBy(d => d).ToArray();

    [Test]
    public void NormalChangesNothing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FSDifficultyRules.HealthMultiplier(FSDifficulty.Normal), Is.EqualTo(1f));
            Assert.That(FSDifficultyRules.CountMultiplier(FSDifficulty.Normal), Is.EqualTo(1f));
            Assert.That(FSDifficultyRules.DamageMultiplier(FSDifficulty.Normal), Is.EqualTo(1f));
            Assert.That(FSDifficultyRules.RewardMultiplier(FSDifficulty.Normal), Is.EqualTo(1f));
            Assert.That(FSDifficultyRules.VictoryBonusPerkPoints(FSDifficulty.Normal), Is.Zero);
        });
    }

    [Test]
    public void EachStepIsHarderAndPaysMore()
    {
        for (var i = 1; i < Ordered.Length; i++)
        {
            var easier = Ordered[i - 1];
            var harder = Ordered[i];
            Assert.Multiple(() =>
            {
                Assert.That(FSDifficultyRules.HealthMultiplier(harder), Is.GreaterThan(FSDifficultyRules.HealthMultiplier(easier)));
                Assert.That(FSDifficultyRules.CountMultiplier(harder), Is.GreaterThan(FSDifficultyRules.CountMultiplier(easier)));
                Assert.That(FSDifficultyRules.DamageMultiplier(harder), Is.GreaterThan(FSDifficultyRules.DamageMultiplier(easier)));
                Assert.That(FSDifficultyRules.RewardMultiplier(harder), Is.GreaterThan(FSDifficultyRules.RewardMultiplier(easier)));
                Assert.That(FSDifficultyRules.VictoryBonusPerkPoints(harder), Is.GreaterThan(FSDifficultyRules.VictoryBonusPerkPoints(easier)));
            });
        }
    }

    [Test]
    public void EveryDifficultyHasItsOwnName()
    {
        Assert.That(Ordered.Select(FSDifficultyRules.NameLocId), Is.Unique);
    }
}
