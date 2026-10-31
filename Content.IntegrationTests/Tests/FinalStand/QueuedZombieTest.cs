#nullable enable
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.FinalStand;

// A wave zombie stuck behind another one on the way to the same target is queued, not lost: it holds still
// instead of giving up on the move, keeps its path, and carries on once the way clears. A corpse ahead
// doesn't count as a zombie to wait for.
// Zombies don't block each other physically and one zombie's spacing doesn't stop another, so two of them alone
// never stall: the one behind walks through. A crowd is what stalls them. The tests put the one behind straight into
// that state once it has its path: it hasn't got anywhere for a while, with the other right ahead.
[TestFixture]
public sealed class QueuedZombieTest : GameTest
{
    private const string Zombie = "FSZombieNormal";
    private const string Wall = "WallSolid";
    private const string Door = "Airlock";

    [TestCase(false)]
    [TestCase(true)]
    public async Task ZombieBehindAnotherHoldsThenMovesOn(bool killFront)
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var htn = entMan.System<HTNSystem>();
        var steeringSystem = entMan.System<NPCSteeringSystem>();
        var mobState = entMan.System<MobStateSystem>();
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

        await Stall(server, entMan, timing, back, front);

        // Well past the three stuck periods after which a move used to give up.
        await server.WaitRunTicks(150);

        var heldX = 0f;
        await server.WaitAssertion(() =>
        {
            var steering = entMan.GetComponent<NPCSteeringComponent>(back);
            heldX = entMan.GetComponent<TransformComponent>(back).LocalPosition.X;
            Assert.Multiple(() =>
            {
                Assert.That(steering.Status, Is.Not.EqualTo(SteeringStatus.NoPath), "the queued zombie gave up on its move");
                Assert.That(steering.QueuedUntil, Is.GreaterThan(timing.CurTime - TimeSpan.FromSeconds(1)),
                    "the queued zombie isn't holding");
                Assert.That(steering.CurrentPath, Is.Not.Empty, "holding threw the path away");
            });

            if (killFront)
                mobState.ChangeMobState(front, MobState.Dead);
            else
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
                Assert.That(x, Is.LessThan(heldX - 0.2f), "didn't move up once the way cleared");
            });
        });
    }

    // The path kept through a hold can go stale: a door put on the target's tile and taken off again rebuilds the
    // grid there and invalidates that node. Reaching it means a new path, not giving up the move.
    [Test]
    public async Task PathInvalidatedDuringHoldIsReplanned()
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

        await Stall(server, entMan, timing, back, front);

        EntityUid blocker = default;
        await server.WaitPost(() =>
            blocker = entMan.SpawnEntity(Door, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f))));
        await server.WaitRunTicks(60);
        await server.WaitPost(() => entMan.DeleteEntity(blocker));
        await server.WaitRunTicks(60);

        var heldX = 0f;

        await server.WaitAssertion(() =>
        {
            var steering = entMan.GetComponent<NPCSteeringComponent>(back);
            Assert.Multiple(() =>
            {
                Assert.That(steering.Status, Is.Not.EqualTo(SteeringStatus.NoPath), "gave up on a stale node while holding");
                Assert.That(steering.QueuedUntil, Is.Not.EqualTo(TimeSpan.Zero), "the zombie should still be holding");
            });

            heldX = entMan.GetComponent<TransformComponent>(back).LocalPosition.X;
            entMan.DeleteEntity(front);
        });

        await server.WaitRunTicks(150);

        await server.WaitAssertion(() =>
        {
            var steering = entMan.GetComponent<NPCSteeringComponent>(back);
            var x = entMan.GetComponent<TransformComponent>(back).LocalPosition.X;
            Assert.Multiple(() =>
            {
                Assert.That(steering.Status, Is.Not.EqualTo(SteeringStatus.NoPath), "gave up on a stale node instead of repathing");
                Assert.That(x, Is.LessThan(heldX - 0.2f), "didn't move on after the stale path");
            });
        });
    }

    // Lets the one behind get its path, puts it right behind the other one and marks it as not having got anywhere for
    // a while, which is how a crowd leaves it. The next steer sees it stuck with a zombie ahead and queues it. Placing
    // it rather than letting it walk up keeps this independent of zombie speed: at full speed it reaches and passes
    // the other one before a fixed wait is up.
    private static async Task Stall(IIntegrationInstance server, IEntityManager entMan, IGameTiming timing, EntityUid back,
        EntityUid front)
    {
        var hasPath = false;
        for (var i = 0; i < 60 && !hasPath; i++)
        {
            await server.WaitRunTicks(1);
            await server.WaitPost(() => hasPath = entMan.GetComponent<NPCSteeringComponent>(back).CurrentPath.Count > 0);
        }

        Assert.That(hasPath, "the zombie behind has no path to keep");

        await server.WaitPost(() =>
        {
            var xforms = entMan.System<SharedTransformSystem>();
            var frontPos = entMan.GetComponent<TransformComponent>(front).LocalPosition;
            xforms.SetLocalPosition(back, frontPos + new Vector2(0.9f, 0f));

            var steering = entMan.GetComponent<NPCSteeringComponent>(back);
            steering.LastStuckCoordinates = entMan.GetComponent<TransformComponent>(back).Coordinates;
            steering.LastStuckTime = timing.CurTime - TimeSpan.FromSeconds(5);
        });

        await server.WaitRunTicks(4);
        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<NPCSteeringComponent>(back).QueuedUntil, Is.Not.EqualTo(TimeSpan.Zero),
                "a stalled zombie with another right ahead didn't queue"));
    }
}
