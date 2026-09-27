using Content.Server._FinalStand.GameTicking.Rules;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class WaveLaneCapTest
{
    [TestCase(0, 2)]
    [TestCase(1, 2)]
    [TestCase(2, 2)]
    [TestCase(3, 3)]
    [TestCase(4, 3)]
    [TestCase(5, int.MaxValue)]
    [TestCase(12, int.MaxValue)]
    public void LanesScaleWithPlayers(int players, int expected)
    {
        Assert.That(WaveEnemySpawningSystem.MaxLanes(new WaveGameRuleComponent(), players), Is.EqualTo(expected));
    }
}
