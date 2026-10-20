#nullable enable
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

// A wave zombie stuck behind another one on the way to the same target is queued, not lost: it holds still
// instead of giving up on the move, and carries on once the way clears.
[TestFixture]
public sealed class QueuedZombieTest : GameTest
{
    private const string Zombie = "FSZombieNormal";
    private const string Wall = "WallSolid";

    [Test]
    public async Task ZombieBehindAnotherHoldsThenMovesOn()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var htn = entMan.System<HTNSystem>();
        var steeringSystem = entMan.System<NPCSteeringSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid front = default;
        EntityUid back = default;
        await server.WaitPost(() =>
        {
            // A one-tile corridor along x, walled in, with the target wall at its west end.
            for (var x = -1; x <= 6; x++)
            {
                for (var y = -1; y <= 1; y++)
                    mapSystem.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            for (var x = -1; x <= 6; x++)
            {
                entMan.SpawnEntity(Wall, new EntityCoordinates(map.Grid, new Vector2(x + 0.5f, 1.5f)));
                entMan.SpawnEntity(Wall, new EntityCoordinates(map.Grid, new Vector2(x + 0.5f, -0.5f)));
            }

            var target = entMan.SpawnEntity(Wall, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));

            front = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(2.5f, 0.5f)));
            back = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(3.6f, 0.5f)));

            foreach (var zombie in new[] { front, back })
            {
                entMan.EnsureComponent<WaveSpawnedTagComponent>(zombie);
                htn.SetHTNEnabled((zombie, entMan.GetComponent<HTNComponent>(zombie)), false);
            }

            var steering = steeringSystem.Register(back, new EntityCoordinates(target, Vector2.Zero));
            steering.Range = 1f;
        });

        // Well past the three stuck periods after which a move used to give up.
        await server.WaitRunTicks(150);

        await server.WaitAssertion(() =>
        {
            var steering = entMan.GetComponent<NPCSteeringComponent>(back);
            Assert.Multiple(() =>
            {
                Assert.That(steering.Status, Is.Not.EqualTo(SteeringStatus.NoPath), "the queued zombie gave up on its move");
                Assert.That(steering.QueuedUntil, Is.GreaterThan(timing.CurTime - TimeSpan.FromSeconds(1)),
                    "the queued zombie isn't holding");
            });

            entMan.DeleteEntity(front);
        });

        await server.WaitRunTicks(150);

        await server.WaitAssertion(() =>
        {
            var steering = entMan.GetComponent<NPCSteeringComponent>(back);
            var x = entMan.GetComponent<TransformComponent>(back).LocalPosition.X;
            Assert.Multiple(() =>
            {
                Assert.That(steering.QueuedUntil, Is.EqualTo(TimeSpan.Zero), "still holding with the way clear");
                Assert.That(x, Is.LessThan(2.5f), "didn't move up once the way cleared");
            });
        });
    }
}
