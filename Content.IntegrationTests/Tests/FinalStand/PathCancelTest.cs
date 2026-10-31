#nullable enable
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared.NPC;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.FinalStand;

// Path requests queue behind each other, 256 a tick at most. A request that's been cancelled must stop costing
// anything and must not touch the steering that gave up on it, and a zombie waiting its turn in a long queue isn't
// stuck.
[TestFixture]
[TestOf(typeof(PathfindingSystem))]
public sealed class PathCancelTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    private const string Zombie = "FSZombieNormal";
    private const int Length = 16;
    private const int NavmeshTicks = 30;
    private const int RequestsPerTick = 256;

    [Test]
    public async Task CancelledRequestFindsNothing()
    {
        var (zombie, map) = await SetUp();
        var server = Pair.Server;
        var pathfinding = server.ResolveDependency<IEntityManager>().System<PathfindingSystem>();

        Task<PathResultEvent> kept = default!;
        Task<PathResultEvent> cancelled = default!;
        await server.WaitPost(() =>
        {
            kept = pathfinding.GetPath(zombie, Tile(map, 0), Tile(map, Length - 2), 0.5f, CancellationToken.None);
            var cts = new CancellationTokenSource();
            cancelled = pathfinding.GetPath(zombie, Tile(map, 0), Tile(map, Length - 2), 0.5f, cts.Token);
            cts.Cancel();
        });

        await server.WaitRunTicks(5);

        Assert.That(kept.IsCompletedSuccessfully && cancelled.IsCompletedSuccessfully, "the requests never finished");
        Assert.That((await kept).Result, Is.EqualTo(PathResult.Path), "the corridor should have a path");
        Assert.That((await cancelled).Result, Is.EqualTo(PathResult.NoPath), "a cancelled request still ran its search");
    }

    // Four seconds of requests ahead of it, past the three seconds steering used to allow before giving up.
    [Test]
    public async Task ZombieWaitsOutALongQueue()
    {
        var (zombie, map) = await SetUp();
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var steering = entMan.System<NPCSteeringSystem>();

        await server.WaitPost(() =>
        {
            Fill(zombie, map, RequestsPerTick * 30 * 4);
            steering.Register(zombie, Tile(map, Length - 2));
        });

        await Pair.RunSeconds(7);

        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<NPCSteeringComponent>(zombie);
            Assert.That(comp.Status, Is.Not.EqualTo(SteeringStatus.NoPath), "the zombie gave up on a path that was coming");
            Assert.That(entMan.GetComponent<TransformComponent>(zombie).Coordinates.X, Is.GreaterThan(4f),
                "the zombie never set off once its path came");
        });
    }

    // A zombie that changes destination while its first request is still queued must end up on the second one.
    [Test]
    public async Task SupersededRequestLeavesTheNewOneAlone()
    {
        var (zombie, map) = await SetUp();
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var steering = entMan.System<NPCSteeringSystem>();

        await server.WaitPost(() =>
        {
            Fill(zombie, map, RequestsPerTick * 5);
            steering.Register(zombie, Tile(map, Length - 2));
        });

        // Long enough for the first request to be made, not to be answered.
        await server.WaitRunTicks(3);
        await server.WaitPost(() =>
        {
            Assert.That(entMan.GetComponent<NPCSteeringComponent>(zombie).Pathfind, "the first request should still be queued");
            steering.Register(zombie, Tile(map, Length - 5));
        });

        await Pair.RunSeconds(2);

        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<NPCSteeringComponent>(zombie);
            Assert.That(comp.FailedPathCount, Is.Zero, "the cancelled request counted against the new destination");
            Assert.That(comp.Status, Is.Not.EqualTo(SteeringStatus.NoPath));
            Assert.That(entMan.GetComponent<TransformComponent>(zombie).Coordinates.X, Is.GreaterThan(2f),
                "the zombie didn't head for its new destination");
        });
    }

    private async Task<(EntityUid Zombie, TestMapData Map)> SetUp()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var htn = entMan.System<HTNSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid zombie = default;
        await server.WaitPost(() =>
        {
            for (var x = -1; x <= Length; x++)
            {
                for (var y = -1; y <= 1; y++)
                    mapSystem.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            zombie = entMan.SpawnEntity(Zombie, Tile(map, 0));
            entMan.EnsureComponent<WaveSpawnedTagComponent>(zombie);
            htn.SetHTNEnabled((zombie, entMan.GetComponent<HTNComponent>(zombie)), false);
        });

        await server.WaitRunTicks(NavmeshTicks);
        return (zombie, map);
    }

    private static EntityCoordinates Tile(TestMapData map, int x) => new(map.Grid, new Vector2(x + 0.5f, 0.5f));

    // Requests that start where they end are answered without a search, so each costs only its turn in the queue.
    private void Fill(EntityUid zombie, TestMapData map, int count)
    {
        var pathfinding = Pair.Server.ResolveDependency<IEntityManager>().System<PathfindingSystem>();
        for (var i = 0; i < count; i++)
            _ = pathfinding.GetPath(zombie, Tile(map, 0), Tile(map, 0), 0.5f, CancellationToken.None);
    }
}
