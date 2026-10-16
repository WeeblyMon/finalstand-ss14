using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class PlayerHpScalingTest : GameTest
{
    [Test]
    public void ZombieHealthGrowsWithTheCrewUpToACap()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WaveEnemyScalingSystem.GetPlayerHpFactor(1), Is.EqualTo(1f));
            Assert.That(WaveEnemyScalingSystem.GetPlayerHpFactor(0), Is.EqualTo(1f), "an empty server scales like a solo one");
            Assert.That(WaveEnemyScalingSystem.GetPlayerHpFactor(5), Is.EqualTo(1.48f).Within(0.001f));
            Assert.That(WaveEnemyScalingSystem.GetPlayerHpFactor(40), Is.EqualTo(WaveEnemyScalingSystem.MaxPlayerHpFactor));
        });
    }

    [Test]
    public async Task ABiggerCrewMeansTougherZombies()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var scaling = SEntMan.System<WaveEnemyScalingSystem>();
            var thresholds = SEntMan.System<MobThresholdSystem>();

            var solo = SEntMan.SpawnEntity("FSZombieNormal", map.GridCoords);
            var crew = SEntMan.SpawnEntity("FSZombieNormal", map.GridCoords);
            scaling.ScaleEnemyHp(solo, 5, 1);
            scaling.ScaleEnemyHp(crew, 5, 6);

            Assert.That(thresholds.TryGetThresholdForState(solo, MobState.Dead, out var soloDead), Is.True);
            Assert.That(thresholds.TryGetThresholdForState(crew, MobState.Dead, out var crewDead), Is.True);
            Assert.That(crewDead!.Value.Float(), Is.EqualTo(soloDead!.Value.Float() * 1.6f).Within(0.5f));
        });
    }
}
