using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Deployables;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

// Runtime deadlines move with a pause. While a map is paused its entities don't update but the clock runs on, so a
// fixed deadline expired the moment an admin unpaused: every placed null field and damage beacon vanished at once.
[TestFixture]
[TestOf(typeof(FSDeployableLifetimeComponent))]
public sealed class PausedDeadlineTest : GameTest
{
    private const string NullField = "FSNullFieldDeployed";

    [Test]
    public async Task PauseDoesNotUseUpALifetime()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid field = default;
        var left = TimeSpan.Zero;
        await server.WaitPost(() =>
        {
            field = entMan.SpawnEntity(NullField, map.GridCoords);
            left = entMan.GetComponent<FSDeployableLifetimeComponent>(field).ExpiresAt - timing.CurTime;
            mapSystem.SetPaused(map.MapId, true);
        });

        Assert.That(left, Is.GreaterThan(TimeSpan.Zero), "the null field had no lifetime to spend");

        // Paused for longer than the field lasts.
        await server.WaitRunTicks((int) (timing.TickRate * (left.TotalSeconds + 2)));
        await server.WaitPost(() => mapSystem.SetPaused(map.MapId, false));
        await server.WaitRunTicks(30);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(field) && !entMan.IsQueuedForDeletion(field),
                "the null field expired while its map was paused");
            var stillLeft = entMan.GetComponent<FSDeployableLifetimeComponent>(field).ExpiresAt - timing.CurTime;
            Assert.That(stillLeft, Is.GreaterThan(left - TimeSpan.FromSeconds(2)), "the pause used up the field's lifetime");
        });
    }
}
