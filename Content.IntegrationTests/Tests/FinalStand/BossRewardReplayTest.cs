using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Server.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

// A boss wave pays everyone a perk point, which persists across rounds. A wave replayed after a lost CCC life spawns
// its boss again, but the point is paid once per wave, or a crew could farm it by letting the CCC drop.
[TestFixture]
[TestOf(typeof(WaveGameRuleSystem))]
public sealed class BossRewardReplayTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        Connected = true,
        DummyTicker = false,
        Map = PoolManager.TestStation,
    };

    private static readonly EntProtoId WaveRule = "WaveSurvivalZombie";

    [Test]
    public async Task ReplayedBossPaysNoSecondPoint()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var ticker = entMan.System<GameTicker>();
        var waves = entMan.System<WaveGameRuleSystem>();
        var wallets = entMan.System<FSPlayerWalletSystem>();
        var store = entMan.System<FSPlayerDataStore>();
        var minds = entMan.System<SharedMindSystem>();
        var mobState = entMan.System<MobStateSystem>();
        var user = Pair.Player!.UserId;

        await server.WaitAssertion(() =>
        {
            var grid = entMan.EntityQueryEnumerator<MapGridComponent>();
            Assert.That(grid.MoveNext(out var gridUid, out _));
            var spawner = entMan.SpawnEntity(null, new EntityCoordinates(gridUid, 0.5f, 0.5f));
            entMan.AddComponent<WaveEnemySpawnerComponent>(spawner);

            var mind = minds.CreateMind(user, "test").Owner;
            var original = store.GetPerkPoints(user.UserId);
            var before = wallets.GetPerkPoints(mind, user);

            Assert.That(ticker.StartGameRule(WaveRule, out var rule));
            var comp = entMan.GetComponent<WaveGameRuleComponent>(rule);

            Assert.That(waves.ForceNextWave(5), Is.EqualTo(5));
            var boss = comp.GiantEntity;
            Assert.That(boss.IsValid(), "the boss wave spawned no boss");
            mobState.ChangeMobState(boss, MobState.Dead);
            Assert.That(wallets.GetPerkPoints(mind, user), Is.EqualTo(before + comp.BossWavePerkReward),
                "killing the boss paid no perk point");

            Assert.That(waves.FailWave(), Is.EqualTo(5));
            Assert.That(waves.ForceNextWave(5), Is.EqualTo(5));
            var replayed = comp.GiantEntity;
            Assert.That(replayed.IsValid(), "the replayed boss wave spawned no boss");
            mobState.ChangeMobState(replayed, MobState.Dead);
            Assert.That(wallets.GetPerkPoints(mind, user), Is.EqualTo(before + comp.BossWavePerkReward),
                "the replayed boss paid its perk point again");

            store.UpsertPerkPoints(user.UserId, "test", original);
        });
    }
}
