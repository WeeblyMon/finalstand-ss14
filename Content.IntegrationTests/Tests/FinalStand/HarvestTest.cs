using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class HarvestTest : GameTest
{
    private const string EnemyProto = "FSZombieNormal";
    private const string ToughEnemyProto = "FSZombieArmoured";
    private const string LauncherProto = "FSChemLauncher";

    [Test]
    public async Task EnemiesDyingNearbyRefillTheReserve()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var launcher = SpawnCarriedLauncher(entMan, map.GridCoords);
            launcher.Reserve = 0f;

            KillEnemyAt(entMan, map.GridCoords, EnemyProto, 1);

            Assert.That(launcher.Reserve, Is.GreaterThan(0f),
                "kills are the only mid-wave refill, so a chemist at the front must never run dry");
        });
    }

    [Test]
    public async Task TougherEnemiesRefillMore()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var launcher = SpawnCarriedLauncher(entMan, map.GridCoords);

            launcher.Reserve = 0f;
            KillEnemyAt(entMan, map.GridCoords, EnemyProto, 1);
            var normal = launcher.Reserve;

            launcher.Reserve = 0f;
            KillEnemyAt(entMan, map.GridCoords, ToughEnemyProto, 1);

            Assert.That(launcher.Reserve, Is.GreaterThan(normal),
                "refill follows the enemy's kill value, so an armoured kill must give more than a shambler");
        });
    }

    [Test]
    public async Task ADroppedLauncherRefillsNothing()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(LauncherProto, map.GridCoords);
            var launcher = entMan.GetComponent<FSChemLauncherComponent>(uid);
            launcher.Reserve = 0f;

            KillEnemyAt(entMan, map.GridCoords, EnemyProto, 5);

            Assert.That(launcher.Reserve, Is.EqualTo(0f),
                "a launcher left in the corridor must not fill up risk-free");
        });
    }

    [Test]
    public async Task DistantDeathsAreNotHarvested()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var launcher = SpawnCarriedLauncher(entMan, map.GridCoords);
            launcher.Reserve = 0f;

            var far = map.GridCoords.Offset(new System.Numerics.Vector2(launcher.HarvestRange * 4f, 0f));
            KillEnemyAt(entMan, far, EnemyProto, 5);

            Assert.That(launcher.Reserve, Is.EqualTo(0f),
                "refill must reward being where the fight is, not hiding in the lab");
        });
    }

    [Test]
    public async Task TheReserveNeverOverfills()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var launcher = SpawnCarriedLauncher(entMan, map.GridCoords);
            launcher.Reserve = 0f;

            KillEnemyAt(entMan, map.GridCoords, EnemyProto, 100);

            Assert.That(launcher.Reserve, Is.EqualTo(launcher.MaxReserve).Within(0.01f));
        });
    }

    [Test]
    public async Task PrepFillsTheReserve()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(LauncherProto, map.GridCoords);
            var launcher = entMan.GetComponent<FSChemLauncherComponent>(uid);
            launcher.Reserve = 0f;

            entMan.EventBus.RaiseEvent(EventSource.Local, new WavePrepStartedEvent());

            Assert.That(launcher.Reserve, Is.EqualTo(launcher.MaxReserve).Within(0.01f),
                "every wave has to start loaded, or the chemist walks back to the bench");
        });
    }

    private static FSChemLauncherComponent SpawnCarriedLauncher(IEntityManager entMan, EntityCoordinates coords)
    {
        var carrier = entMan.SpawnEntity("MobHuman", coords);
        entMan.EnsureComponent<FSFriendlyFireComponent>(carrier);

        var launcher = entMan.SpawnEntity(LauncherProto, coords);
        entMan.GetComponent<TransformComponent>(launcher).AttachParent(carrier);

        return entMan.GetComponent<FSChemLauncherComponent>(launcher);
    }

    private static void KillEnemyAt(IEntityManager entMan, EntityCoordinates coords, string proto, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var enemy = entMan.SpawnEntity(proto, coords);
            entMan.EnsureComponent<WaveSpawnedTagComponent>(enemy);

            var state = entMan.GetComponent<MobStateComponent>(enemy);
            var ev = new MobStateChangedEvent(enemy, state, MobState.Alive, MobState.Dead, null);
            entMan.EventBus.RaiseLocalEvent(enemy, ev, true);

            entMan.DeleteEntity(enemy);
        }
    }
}
