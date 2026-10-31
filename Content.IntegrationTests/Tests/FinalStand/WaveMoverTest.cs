#nullable enable
using System;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.CombatMode;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.FinalStand;

// Things that change a wave zombie's state (and so what goes out to every client) when nothing a player can see has
// changed: facing wobbling a few degrees, a creep slower than friction acts on, a combat toggle action nobody uses.
[TestFixture]
[TestOf(typeof(SharedMoverController))]
public sealed class WaveMoverTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    private const string Zombie = "FSZombieNormal";

    // Heading a few degrees off east, it faces exactly one of its sixteen headings rather than the way it walks.
    [Test]
    public async Task ZombieFacesTheNearestHeading()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var htn = entMan.System<HTNSystem>();
        var steering = entMan.System<NPCSteeringSystem>();
        var xforms = entMan.System<SharedTransformSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid zombie = default;
        await server.WaitPost(() =>
        {
            for (var x = 0; x <= 14; x++)
            {
                for (var y = 0; y <= 2; y++)
                    mapSystem.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            zombie = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(0.5f, 1.0f)));
            entMan.EnsureComponent<WaveSpawnedTagComponent>(zombie);
            htn.SetHTNEnabled((zombie, entMan.GetComponent<HTNComponent>(zombie)), false);
        });

        await server.WaitRunTicks(30);
        await server.WaitPost(() => steering.Register(zombie, new EntityCoordinates(map.Grid, new Vector2(13.5f, 2.0f))));
        await Pair.RunSeconds(1);

        await server.WaitAssertion(() =>
        {
            var steps = entMan.GetComponent<FSCoarseFacingComponent>(zombie).Steps;
            var turns = xforms.GetWorldRotation(zombie).Theta / (Math.Tau / steps);
            Assert.That(turns, Is.EqualTo(Math.Round(turns)).Within(1e-4), "the zombie faced the exact way it was walking");
        });
    }

    // Let go of at a crawl slower than friction acts on, it stops instead of creeping on for good.
    [Test]
    public async Task CreepBelowFrictionStops()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var physics = entMan.System<SharedPhysicsSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid zombie = default;
        await server.WaitPost(() =>
        {
            zombie = entMan.SpawnEntity(Zombie, map.GridCoords);
            entMan.EnsureComponent<ActiveInputMoverComponent>(zombie);
        });
        await server.WaitRunTicks(2);

        await server.WaitPost(() =>
        {
            var crawl = entMan.GetComponent<MovementSpeedModifierComponent>(zombie).MinimumFrictionSpeed * 0.9f;
            physics.SetLinearVelocity(zombie, new Vector2(crawl, 0f));
        });
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<PhysicsComponent>(zombie).LinearVelocity, Is.EqualTo(Vector2.Zero),
                "the zombie kept creeping"));
    }

    // Wave enemies spawn without the combat-mode toggle action; a crew member still gets one.
    [TestCase(Zombie, false)]
    [TestCase("MobHuman", true)]
    public async Task CombatToggleActionOnlyForThoseWhoToggle(string proto, bool hasAction)
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var mob = entMan.SpawnEntity(proto, map.GridCoords);
            Assert.That(entMan.GetComponent<CombatModeComponent>(mob).CombatToggleActionEntity.HasValue, Is.EqualTo(hasAction));
        });
    }
}
