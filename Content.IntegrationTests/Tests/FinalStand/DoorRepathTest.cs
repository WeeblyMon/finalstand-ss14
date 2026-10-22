using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.NPC;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
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

            Assert.Multiple(() =>
            {
                Assert.That(steering.CurrentPath, Is.Not.Empty, "a door opening nearby threw the path away");
                Assert.That(steering.RepathRequested, "a door opening nearby didn't ask for a new path");
            });
        });

        await server.WaitRunTicks(30);

        await server.WaitAssertion(() =>
        {
            var steering = entMan.GetComponent<NPCSteeringComponent>(zombie);
            Assert.That(steering.RepathRequested, Is.False, "the requested repath never ran");
            // A delivered path replaces the queue, so the same one means the pathfinder was never asked.
            Assert.That(steering.CurrentPath, Is.Not.SameAs(oldPath), "the requested repath never planned a new path");
            Assert.That(steering.CurrentPath, Is.Not.Empty, "the repath left the zombie without a path");
        });
    }
}
