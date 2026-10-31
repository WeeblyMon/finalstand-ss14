#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.NPC;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

// A zombie pressed against a locker picks it out of its own physics contacts, but only the one it's heading into.
// One smashing its way along a path hits what blocks the next step, not the walls either side of it.
[TestFixture]
[TestOf(typeof(FSObstacleSmashSystem))]
[TestOf(typeof(NPCSteeringSystem))]
public sealed class ObstacleSmashTest : GameTest
{
    // A godmode body on an airless map changes only owner-only state, which PVS-off sending logs as an error.
    public override PoolSettings PoolSettings => new() { Connected = false };

    private const string Zombie = "FSZombieNormal";
    private const string Locker = "ClosetMaintenance";
    private const string Wall = "WallSolid";
    private const string Barricade = "FSBarricadePlasteel";
    private const int NavmeshTicks = 30;
    private const int SmashSeconds = 20;

    [Test]
    public async Task FindsTheLockerItIsPressedAgainst()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid zombie = default;
        EntityUid locker = default;
        await server.WaitPost(() =>
        {
            for (var x = -1; x <= 2; x++)
            {
                mapSystem.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            }

            // Overlapping by a few centimetres, so the first physics step finds them touching.
            zombie = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            locker = entMan.SpawnEntity(Locker, new EntityCoordinates(map.Grid, new Vector2(0.95f, 0.5f)));
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var smash = entMan.System<FSObstacleSmashSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(smash.TryFindObstacle(zombie, Vector2.UnitX, out var ahead), Is.True);
                Assert.That(ahead, Is.EqualTo(locker));
                Assert.That(smash.TryFindObstacle(zombie, -Vector2.UnitX, out _), Is.False, "the locker is behind it");
            });
        });
    }

    [Test]
    public async Task SmashingHitsTheBarricadeNotTheWalls()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var htn = entMan.System<HTNSystem>();
        var steering = entMan.System<NPCSteeringSystem>();
        var map = await Pair.CreateTestMap();

        var walls = new List<EntityUid>();
        EntityUid zombie = default;
        EntityUid barricade = default;
        await server.WaitPost(() =>
        {
            for (var x = -1; x <= 8; x++)
            {
                for (var y = -1; y <= 1; y++)
                    mapSystem.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            // A one-tile-wide corridor, closed at the west end, with a barricade one tile east of the zombie.
            for (var x = -1; x <= 8; x++)
            {
                walls.Add(entMan.SpawnEntity(Wall, new EntityCoordinates(map.Grid, new Vector2(x + 0.5f, 1.5f))));
                walls.Add(entMan.SpawnEntity(Wall, new EntityCoordinates(map.Grid, new Vector2(x + 0.5f, -0.5f))));
            }

            walls.Add(entMan.SpawnEntity(Wall, new EntityCoordinates(map.Grid, new Vector2(-0.5f, 0.5f))));
            barricade = entMan.SpawnEntity(Barricade, new EntityCoordinates(map.Grid, new Vector2(1.5f, 0.5f)));

            zombie = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            htn.SetHTNEnabled((zombie, entMan.GetComponent<HTNComponent>(zombie)), false);
        });

        // A path asked for before the navmesh is built comes back empty.
        await server.WaitRunTicks(NavmeshTicks);
        await server.WaitPost(() => steering.Register(zombie, new EntityCoordinates(map.Grid, new Vector2(6.5f, 0.5f))));

        // About twenty swings. With the touching walls in the lookup, roughly two in seven went into them.
        await Pair.RunSeconds(SmashSeconds);

        await server.WaitAssertion(() =>
        {
            Assert.That(!entMan.EntityExists(barricade) || entMan.GetComponent<DamageableComponent>(barricade).TotalDamage > 0,
                "the zombie never hit the barricade");

            foreach (var wall in walls)
            {
                Assert.That(entMan.GetComponent<DamageableComponent>(wall).TotalDamage.Float(), Is.Zero,
                    $"the zombie hit the wall at {entMan.GetComponent<TransformComponent>(wall).LocalPosition}");
            }
        });
    }

    // A zombie fighting its way to someone swings at what's in the way and carries on fighting. Smashing used to
    // switch combat mode off afterwards, which dropped the fight it was in. The wall is on the zombie's path
    // (steering breaks it); the locker turns up after the path is planned (obstacle smash breaks it).
    [TestCase(false)]
    [TestCase(true)]
    public async Task SmashingKeepsTheFightGoing(bool lockerInTheWay)
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var htn = entMan.System<HTNSystem>();
        var combat = entMan.System<SharedCombatModeSystem>();
        var godmode = entMan.System<SharedGodmodeSystem>();
        var map = await Pair.CreateTestMap();

        // A one-tile-wide corridor; off the tiles is space.
        EntityUid zombie = default;
        EntityUid defender = default;
        EntityUid obstacle = default;
        await server.WaitPost(() =>
        {
            for (var x = 0; x <= 8; x++)
                mapSystem.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);

            defender = entMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, new Vector2(7.5f, 0.5f)));
            godmode.EnableGodmode(defender);
            if (!lockerInTheWay)
                obstacle = entMan.SpawnEntity(Wall, new EntityCoordinates(map.Grid, new Vector2(3.5f, 0.5f)));

            zombie = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<WaveSpawnedTagComponent>(zombie);
            htn.SetHTNEnabled((zombie, entMan.GetComponent<HTNComponent>(zombie)), false);
        });

        await server.WaitRunTicks(NavmeshTicks);

        // What the melee task sets up.
        await server.WaitPost(() =>
        {
            combat.SetInCombatMode(zombie, true);
            entMan.AddComponent<NPCMeleeCombatComponent>(zombie).Target = defender;
        });

        if (lockerInTheWay)
        {
            await server.WaitRunTicks(10);
            await server.WaitPost(() =>
                obstacle = entMan.SpawnEntity(Locker, new EntityCoordinates(map.Grid, new Vector2(3.5f, 0.5f))));
        }

        for (var tick = 0; tick < 6 * 30; tick += 3)
        {
            await server.WaitRunTicks(3);
            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.HasComponent<NPCMeleeCombatComponent>(zombie) && combat.IsInCombatMode(zombie),
                    $"the zombie dropped its fight after {tick + 3} ticks");
            });
        }

        await server.WaitAssertion(() =>
            Assert.That(!entMan.EntityExists(obstacle) || entMan.GetComponent<DamageableComponent>(obstacle).TotalDamage > 0,
                "the zombie never hit what was in its way"));
    }

    // Held in a queue behind another zombie, it isn't pushing against the locker in front of it, whichever way it
    // was last heading.
    [Test]
    public async Task QueuedZombieLeavesTheLockerAlone()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var htn = entMan.System<HTNSystem>();
        var steering = entMan.System<NPCSteeringSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await Pair.CreateTestMap();

        EntityUid zombie = default;
        await server.WaitPost(() =>
        {
            for (var x = -1; x <= 8; x++)
                mapSystem.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);

            zombie = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<WaveSpawnedTagComponent>(zombie);
            htn.SetHTNEnabled((zombie, entMan.GetComponent<HTNComponent>(zombie)), false);
        });

        await server.WaitRunTicks(NavmeshTicks);
        await server.WaitPost(() => steering.Register(zombie, new EntityCoordinates(map.Grid, new Vector2(7.5f, 0.5f))));
        await server.WaitRunTicks(10);

        EntityUid locker = default;
        await server.WaitPost(() =>
        {
            var comp = entMan.GetComponent<NPCSteeringComponent>(zombie);
            Assert.That(comp.LastSteerDirection.X, Is.GreaterThan(0.5f), "the zombie should have set off east");
            comp.QueuedUntil = timing.CurTime + TimeSpan.FromHours(1);

            // Touching it, straight ahead.
            var at = entMan.GetComponent<TransformComponent>(zombie).Coordinates.Position;
            locker = entMan.SpawnEntity(Locker, new EntityCoordinates(map.Grid, at + new Vector2(0.45f, 0f)));
        });

        await Pair.RunSeconds(3);

        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<DamageableComponent>(locker).TotalDamage.Float(), Is.Zero,
                "a queued zombie smashed the locker in front of it"));
    }
}
