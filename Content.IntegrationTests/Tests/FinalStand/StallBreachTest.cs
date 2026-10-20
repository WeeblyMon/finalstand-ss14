using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.NPC;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.NPC;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.FinalStand;

// A wave zombie with nothing to chase that can't make progress picks a structure to break out through, unless a
// door is right there to use instead.
[TestFixture]
[TestOf(typeof(FSBreachTargetSystem))]
public sealed class StallBreachTest : GameTest
{
    private const string Zombie = "FSZombieNormal";
    private const string Wall = "WallSolid";
    private const string Door = "Airlock";

    [TestCase(false)]
    [TestCase(true)]
    public async Task BoxedInZombieBreachesUnlessADoorIsNear(bool door)
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid zombie = default;
        await server.WaitPost(() =>
        {
            // Floor well past the cell: walls next to space are never picked, so the hull stays shut.
            for (var x = -3; x <= 3; x++)
            {
                for (var y = -3; y <= 3; y++)
                    mapSystem.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            // A one-tile cell; one side is a door when testing the door case.
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    var proto = door && x == 1 && y == 0 ? Door : Wall;
                    entMan.SpawnEntity(proto, new EntityCoordinates(map.Grid, new Vector2(x + 0.5f, y + 0.5f)));
                }
            }

            zombie = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<WaveSpawnedTagComponent>(zombie);
        });

        await server.WaitRunTicks(300);

        await server.WaitAssertion(() =>
        {
            var blackboard = entMan.GetComponent<HTNComponent>(zombie).Blackboard;
            var picked = blackboard.ContainsKey(FSAIBlackboardKeys.BreachTarget);
            if (door)
                Assert.That(picked, Is.False, "picked a wall to break with a door right there");
            else
                Assert.That(picked, "a boxed-in zombie never picked anything to break");
        });
    }
}
