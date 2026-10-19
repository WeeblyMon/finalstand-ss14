#nullable enable
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.NPC;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.FinalStand;

// A zombie pressed against a locker picks it out of its own physics contacts, but only the one it's heading into.
[TestFixture]
public sealed class ObstacleSmashTest : GameTest
{
    private const string Zombie = "FSZombieNormal";
    private const string Locker = "ClosetMaintenance";

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
}
