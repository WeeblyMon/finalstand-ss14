#nullable enable
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Threading;
using Content.IntegrationTests.Fixtures;
using Content.Server.NPC.Pathfinding;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.FinalStand;

// A search too big for one tick's time slice carries on next tick. The node limit is for the whole search: one
// resumed a few nodes short of it has to give up a few nodes later, not count afresh and flood the grid.
[TestFixture]
[TestOf(typeof(PathfindingSystem))]
public sealed class PathNodeLimitTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [Test]
    public async Task ResumedSearchStopsAtTheLimit()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var pathfinding = entMan.System<PathfindingSystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            for (var x = 0; x <= 16; x++)
                mapSystem.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
        });
        await server.WaitRunTicks(30);

        await server.WaitAssertion(() =>
        {
            var type = typeof(PathfindingSystem);
            var nodeLimit = (int) type.GetField("NodeLimit", Private)!.GetRawConstantValue()!;
            var comparer = (IComparer<(float, PathPoly)>) type.GetField("PathPolyComparer", Private)!.GetValue(null)!;
            var update = type.GetMethod("UpdateAStarPath", Private)!;

            var start = new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f));
            var end = new EntityCoordinates(map.Grid, new Vector2(16.5f, 0.5f));

            PathResult Search(int alreadyExpanded)
            {
                var request = new AStarPathRequest(start, end, PathFlags.None, 0.5f,
                    PathfindingSystem.PathfindingCollisionLayer, PathfindingSystem.PathfindingCollisionMask,
                    CancellationToken.None);

                // As a previous slice would have left it: started, with the start node waiting in the frontier.
                var startPoly = pathfinding.GetPoly(start)!;
                request.Started = true;
                request.Frontier = new PriorityQueue<(float, PathPoly)>(comparer);
                request.Frontier.Add((0f, startPoly));
                request.CostSoFar[startPoly] = 0f;
                request.Expanded = alreadyExpanded;

                return (PathResult) update.Invoke(pathfinding, new object[] { request })!;
            }

            Assert.That(Search(1), Is.EqualTo(PathResult.Path), "the corridor should have a path");
            Assert.That(Search(nodeLimit - 5), Is.EqualTo(PathResult.NoPath),
                "a search resumed five nodes short of the limit carried on past it");
        });
    }

    // The wander search spreads out to a node limit of its own before picking somewhere it reached.
    [Test]
    public async Task ResumedWanderStopsAtItsLimit()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var pathfinding = entMan.System<PathfindingSystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            for (var x = 0; x <= 16; x++)
                mapSystem.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
        });
        await server.WaitRunTicks(30);

        await server.WaitAssertion(() =>
        {
            var type = typeof(PathfindingSystem);
            var comparer = (IComparer<(float, PathPoly)>) type.GetField("PathPolyComparer", Private)!.GetValue(null)!;
            var update = type.GetMethod("UpdateBFSPath", Private)!;
            var random = server.ResolveDependency<IRobustRandom>();
            const int limit = 200;

            var start = new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f));
            var request = new BFSPathRequest(30f, limit, start, PathFlags.None,
                PathfindingSystem.PathfindingCollisionLayer, PathfindingSystem.PathfindingCollisionMask,
                CancellationToken.None);

            var startPoly = pathfinding.GetPoly(start)!;
            request.Started = true;
            request.Frontier = new PriorityQueue<(float, PathPoly)>(comparer);
            request.Frontier.Add((0f, startPoly));
            request.CostSoFar[startPoly] = 0f;
            request.Expanded = limit - 2;

            update.Invoke(pathfinding, new object[] { random, request });

            // Two more expansions reach a handful of neighbours, nowhere near the corridor's few hundred nodes.
            Assert.That(request.Expanded, Is.EqualTo(limit), "the resumed wander didn't stop at its limit");
            Assert.That(request.CostSoFar.Count, Is.LessThan(20), "the resumed wander kept spreading past its limit");
        });
    }
}
