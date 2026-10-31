using System;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.NPC;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.FinalStand;

// A door opening near the horde makes the zombies around it plan again, since the way may be shorter now. They
// keep walking their old path until the new one comes back; dropping it stood the whole crowd still meanwhile.
[TestFixture]
[TestOf(typeof(FSBreachTargetSystem))]
public sealed class DoorRepathTest : GameTest
{
    private const string Zombie = "FSZombieNormal";
    private const string Door = "Airlock";

    [Test]
    public async Task OpeningADoorKeepsThePathWhileReplanning()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var htn = entMan.System<HTNSystem>();
        var steeringSystem = entMan.System<NPCSteeringSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid zombie = default;
        EntityUid door = default;
        await server.WaitPost(() =>
        {
            for (var x = -1; x <= 20; x++)
            {
                for (var y = -1; y <= 1; y++)
                    mapSystem.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            zombie = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<WaveSpawnedTagComponent>(zombie);
            htn.SetHTNEnabled((zombie, entMan.GetComponent<HTNComponent>(zombie)), false);
            steeringSystem.Register(zombie, new EntityCoordinates(map.Grid, new Vector2(19.5f, 0.5f)));

            door = entMan.SpawnEntity(Door, new EntityCoordinates(map.Grid, new Vector2(2.5f, -0.5f)));
        });

        await server.WaitRunTicks(30);

        object oldPath = null;
        await server.WaitAssertion(() =>
        {
            var steering = entMan.GetComponent<NPCSteeringComponent>(zombie);
            Assert.That(steering.CurrentPath, Is.Not.Empty, "the zombie never got a path to start with");
            oldPath = steering.CurrentPath;

            entMan.EventBus.RaiseLocalEvent(door, new DoorStateChangedEvent(DoorState.Open));
        });

        // Nothing on the grid changed, so the navmesh is already current and the replan goes out a tick after the
        // one it was raised in. Steering may have taken the request up in the same tick.
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            var steering = entMan.GetComponent<NPCSteeringComponent>(zombie);
            Assert.Multiple(() =>
            {
                Assert.That(steering.CurrentPath, Is.Not.Empty, "a door opening nearby threw the path away");
                Assert.That(steering.RepathRequested || steering.Pathfind || !ReferenceEquals(steering.CurrentPath, oldPath),
                    "a door opening nearby didn't ask for a new path");
            });
        });

        await server.WaitRunTicks(28);

        await server.WaitAssertion(() =>
        {
            var steering = entMan.GetComponent<NPCSteeringComponent>(zombie);
            Assert.That(steering.RepathRequested, Is.False, "the requested repath never ran");
            // A delivered path replaces the queue, so the same one means the pathfinder was never asked.
            Assert.That(steering.CurrentPath, Is.Not.SameAs(oldPath), "the requested repath never planned a new path");
            Assert.That(steering.CurrentPath, Is.Not.Empty, "the repath left the zombie without a path");
        });
    }

    // A real airlock. The way round is twice as long as through it, and the zombie, which can't break doors here,
    // sets off round before the door opens. Its path never touches the door tile, so only the replan the opening
    // asks for can turn it back, and that only helps once the navmesh knows the door is open.
    [Test]
    public async Task ZombieTurnsBackThroughADoorThatOpened()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var htn = entMan.System<HTNSystem>();
        var steeringSystem = entMan.System<NPCSteeringSystem>();
        var doors = entMan.System<SharedDoorSystem>();
        var map = await Pair.CreateTestMap();

        // A corridor along y = 0 with the door half way, and a loop round through y = 4; off the tiles is space.
        // Laid in walking order, as a tile with no neighbour yet splits off into a grid of its own.
        EntityUid zombie = default;
        EntityUid door = default;
        await server.WaitPost(() =>
        {
            for (var x = 0; x <= 8; x++)
                mapSystem.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            for (var y = 1; y <= 4; y++)
                mapSystem.SetTile(map.Grid, new Vector2i(0, y), map.Tile.Tile);
            for (var x = 1; x <= 8; x++)
                mapSystem.SetTile(map.Grid, new Vector2i(x, 4), map.Tile.Tile);
            for (var y = 3; y >= 1; y--)
                mapSystem.SetTile(map.Grid, new Vector2i(8, y), map.Tile.Tile);

            door = entMan.SpawnEntity(Door, new EntityCoordinates(map.Grid, new Vector2(4.5f, 0.5f)));
            zombie = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<WaveSpawnedTagComponent>(zombie);
            var zombieHtn = entMan.GetComponent<HTNComponent>(zombie);
            zombieHtn.Blackboard.SetValue(NPCBlackboard.NavSmash, false);
            htn.SetHTNEnabled((zombie, zombieHtn), false);
        });

        await server.WaitRunTicks(30);
        await server.WaitPost(() => steeringSystem.Register(zombie, new EntityCoordinates(map.Grid, new Vector2(8.5f, 0.5f))));
        await server.WaitRunTicks(5);
        await server.WaitPost(() =>
        {
            Assert.That(entMan.GetComponent<NPCSteeringComponent>(zombie).CurrentPath, Is.Not.Empty, "the zombie never got a path round");
            doors.StartOpening(door);
        });

        var throughDoor = false;
        var highest = 0f;
        for (var tick = 0; tick < 6 * 30 && !throughDoor; tick += 2)
        {
            await server.WaitRunTicks(2);
            await server.WaitPost(() =>
            {
                var at = entMan.GetComponent<TransformComponent>(zombie).Coordinates.Position;
                highest = MathF.Max(highest, at.Y);
                throughDoor = at.Y < 1f && at.X > 3.6f && at.X < 5.4f;
            });
        }

        Assert.That(throughDoor, $"the zombie kept to the way round (got as far up as y = {highest:0.0})");
    }
}
