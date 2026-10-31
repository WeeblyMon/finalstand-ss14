using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

// Wave zombies are steered every other tick, and their move input has to carry over the tick in between. The mover
// drops input older than the current tick, so each skipped tick was a coast: the horde ran a few percent under the
// speeds its prototypes are tuned to.
[TestFixture]
[TestOf(typeof(NPCSteeringSystem))]
public sealed class SteeringSpeedTest : GameTest
{
    private const string Zombie = "FSZombieNormal";

    [Test]
    public async Task WaveZombieRunsAtItsSpeed()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var xforms = entMan.System<SharedTransformSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid zombie = default;
        await server.WaitPost(() =>
        {
            for (var x = -2; x <= 60; x++)
            {
                for (var y = -1; y <= 1; y++)
                    mapSystem.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }
        });

        // Let the navmesh build before asking for a path.
        await server.WaitRunTicks(30);

        await server.WaitPost(() =>
        {
            zombie = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            entMan.System<HTNSystem>().SetHTNEnabled((zombie, entMan.GetComponent<HTNComponent>(zombie)), false);
            entMan.System<NPCSteeringSystem>().Register(zombie, new EntityCoordinates(map.Grid, new Vector2(59.5f, 0.5f)));
        });

        // Up to speed first.
        await server.WaitRunTicks(30);

        var start = Vector2.Zero;
        await server.WaitPost(() => start = xforms.GetWorldPosition(zombie));

        var ticks = (int) timing.TickRate * 3;
        await server.WaitRunTicks(ticks);

        await server.WaitAssertion(() =>
        {
            var travelled = (xforms.GetWorldPosition(zombie) - start).Length();
            var speed = travelled / (ticks * (float) timing.TickPeriod.TotalSeconds);
            var tuned = entMan.GetComponent<MovementSpeedModifierComponent>(zombie).CurrentSprintSpeed;
            Assert.That(speed, Is.GreaterThan(tuned * 0.985f), $"ran at {speed:0.00} against a tuned {tuned:0.00}");
        });
    }
}
