using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
[TestOf(typeof(WaveGameRuleSystem))]
public sealed class WaveLifecycleTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Map = PoolManager.TestStation,
    };

    private static readonly EntProtoId WaveRule = "WaveSurvivalZombie";

    // Prep -> forced dark wave -> countdown -> dark combat -> CCC life lost -> prep again -> rule ended.
    [Test]
    public async Task WaveRuleRunsThroughItsPhases()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var ticker = entMan.System<GameTicker>();
        var waves = entMan.System<WaveGameRuleSystem>();
        var darkWaves = entMan.System<FSDarkWaveSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid rule = default;
        await server.WaitPost(() =>
        {
            Assert.That(ticker.StartGameRule(WaveRule, out rule));
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(waves.TryGetWaveStatus(out var wave, out var phase));
            Assert.That(wave, Is.EqualTo(1));
            Assert.That(phase, Is.EqualTo(WavePhase.Prep));

            Assert.That(darkWaves.TryForce(out var armedNow));
            Assert.That(armedNow, "forcing during prep should arm the coming wave");

            waves.RequestWaveStart();
            Assert.That(entMan.GetComponent<WaveGameRuleComponent>(rule).VoteCountdownActive);
        });

        await Pair.RunTicksSync(timing.TickRate * 11);

        await server.WaitAssertion(() =>
        {
            Assert.That(waves.TryGetWaveStatus(out var wave, out var phase));
            Assert.That(phase, Is.EqualTo(WavePhase.Combat));
            Assert.That(wave, Is.EqualTo(1));
            Assert.That(darkWaves.IsDarkWaveActive());

            Assert.That(waves.FailWave(), Is.EqualTo(1));
            Assert.That(waves.TryGetWaveStatus(out wave, out phase));
            Assert.That(phase, Is.EqualTo(WavePhase.Prep), "losing a CCC life sends the round back to prep");
            Assert.That(wave, Is.EqualTo(1), "the failed wave is replayed, not skipped");
            Assert.That(darkWaves.IsDarkWaveActive(), Is.False, "leaving combat has to lift the blackout");
        });

        await Pair.RunTicksSync(timing.TickRate * 2);

        await server.WaitPost(() =>
        {
            ticker.EndGameRule(rule);
        });

        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(waves.TryGetWaveStatus(out _, out _), Is.False);
        });
    }

    // A restart deletes every enemy while the rule is still active. The last one going mustn't clear the wave
    // and start prep (spawning sounds and kits) in the middle of the entity flush.
    [Test]
    public async Task RestartCleanupDoesNotClearTheWave()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var ticker = entMan.System<GameTicker>();
        var waves = entMan.System<WaveGameRuleSystem>();
        var spawning = entMan.System<WaveEnemySpawningSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid enemy = default;
        await server.WaitPost(() =>
        {
            Assert.That(ticker.StartGameRule(WaveRule, out var rule));
            Assert.That(waves.ForceNextWave(), Is.EqualTo(1));

            // The whole wave is out and one enemy is left standing.
            var comp = entMan.GetComponent<WaveGameRuleComponent>(rule);
#pragma warning disable RA0002
            comp.EnemiesSpawnedThisWave = comp.EnemyTotalThisWave;
            enemy = spawning.SpawnWaveEnemy("FSZombieNormal", map.GridCoords, comp);
            comp.AliveEnemies.Add(enemy);
#pragma warning restore RA0002
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(waves.TryGetWaveStatus(out _, out var phase));
            Assert.That(phase, Is.EqualTo(WavePhase.Combat));

            entMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            entMan.DeleteEntity(enemy);

            Assert.That(waves.TryGetWaveStatus(out _, out phase));
            Assert.That(phase, Is.EqualTo(WavePhase.Combat), "the restart flush cleared the wave");
        });
    }
}
