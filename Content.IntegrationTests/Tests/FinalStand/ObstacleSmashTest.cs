#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Damage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.FinalStand;

// A zombie pressed against a locker picks it out of its own physics contacts, but only the one it's heading into.
// One smashing its way along a path hits what blocks the next step, not the walls either side of it.
[TestFixture]
[TestOf(typeof(FSObstacleSmashSystem))]
[TestOf(typeof(NPCSteeringSystem))]
public sealed class ObstacleSmashTest : GameTest
{
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
}
