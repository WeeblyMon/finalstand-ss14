using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class HarvestTest : GameTest
{
    private const string EnemyProto = "FSZombieNormal";
    private const string SatchelProto = "FSHarvestSatchel";

    [Test]
    public async Task EnemiesDyingNearbyFillTheSatchel()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var satchel = SpawnCarriedSatchel(entMan, map.GridCoords, out _);

            Assert.That(Biomass(entMan, satchel), Is.EqualTo(0f), "starts empty");

            KillEnemyAt(entMan, map.GridCoords, 3);

            Assert.That(Biomass(entMan, satchel), Is.GreaterThan(0f),
                "no corpses, no potions - the loop starts here");
        });
    }

    [Test]
    public async Task ADroppedSatchelHarvestsNothing()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var satchel = entMan.SpawnEntity(SatchelProto, map.GridCoords);

            KillEnemyAt(entMan, map.GridCoords, 5);

            Assert.That(Biomass(entMan, satchel), Is.EqualTo(0f),
                "leaving a bag in the corridor must not farm the wave risk-free");
        });
    }

    [Test]
    public async Task HarvestStopsAtThePerWaveCap()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var satchel = SpawnCarriedSatchel(entMan, map.GridCoords, out _);
            var comp = entMan.GetComponent<FSHarvestSatchelComponent>(satchel);

            KillEnemyAt(entMan, map.GridCoords, 200);

            Assert.That(Biomass(entMan, satchel), Is.EqualTo(comp.PerWaveCap).Within(0.01f),
                "the cap is what breaks the kills -> potions -> kills flywheel, so it is not tuning");
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
            var satchel = SpawnCarriedSatchel(entMan, map.GridCoords, out _);
            var comp = entMan.GetComponent<FSHarvestSatchelComponent>(satchel);

            var far = map.GridCoords.Offset(new System.Numerics.Vector2(comp.Range * 4f, 0f));
            KillEnemyAt(entMan, far, 5);

            Assert.That(Biomass(entMan, satchel), Is.EqualTo(0f),
                "harvest must reward being where the fight is, not hiding in the lab");
        });
    }

    private static EntityUid SpawnCarriedSatchel(IEntityManager entMan, EntityCoordinates coords, out EntityUid carrier)
    {
        carrier = entMan.SpawnEntity("MobHuman", coords);
        entMan.EnsureComponent<Content.Shared._FinalStand.FriendlyFire.FSFriendlyFireComponent>(carrier);

        var satchel = entMan.SpawnEntity(SatchelProto, coords);
        entMan.GetComponent<TransformComponent>(satchel).AttachParent(carrier);

        return satchel;
    }

    private static float Biomass(IEntityManager entMan, EntityUid satchel)
    {
        var solutions = entMan.System<SharedSolutionContainerSystem>();
        if (!solutions.TryGetSolution(satchel, "satchel", out _, out var solution))
            return 0f;

        return solution.GetTotalPrototypeQuantity("FSBiomass").Float();
    }

    private static void KillEnemyAt(IEntityManager entMan, EntityCoordinates coords, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var enemy = entMan.SpawnEntity(EnemyProto, coords);
            entMan.EnsureComponent<WaveSpawnedTagComponent>(enemy);

            var state = entMan.GetComponent<MobStateComponent>(enemy);
            var ev = new MobStateChangedEvent(enemy, state, MobState.Alive, MobState.Dead, null);
            entMan.EventBus.RaiseLocalEvent(enemy, ev, true);

            entMan.DeleteEntity(enemy);
        }
    }
}
