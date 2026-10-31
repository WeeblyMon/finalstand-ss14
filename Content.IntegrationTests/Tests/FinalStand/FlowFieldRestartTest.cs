using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.NPC;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// The horde flow field is built around the CCC and names that round's grids. A restart drops it at once: kept until
// the next rebuild, a zombie on the new map read as unreachable and, without a path, was moved off or deleted.
[TestFixture]
[TestOf(typeof(HordeFlowFieldSystem))]
public sealed class FlowFieldRestartTest : GameTest
{
    // Dirty: the restart cleanup is raised by hand, which every system answers.
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task RestartDropsTheField()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var flow = entMan.System<HordeFlowFieldSystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitPost(() => entMan.SpawnEntity("FinalStandCCC", map.GridCoords));

        // Rebuilt a few seconds after the CCC turns up.
        var built = false;
        for (var i = 0; i < 20 && !built; i++)
        {
            await Pair.RunSeconds(0.5f);
            await server.WaitPost(() => built = flow.HasField);
        }

        Assert.That(built, "the flow field never built around the CCC");

        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Assert.That(flow.HasField, Is.False, "the old round's flow field outlived the restart");
        });
    }
}
