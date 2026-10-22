using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Research;
using Content.Server.Research.Systems;
using Content.Shared._FinalStand.Research.Components;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// Reading the research state never creates it. Every attach sends the player their personal research and a console
// linking to a server syncs it; both used to create the state when there was none, so the ghost made while
// "entities delete" ran left a new state entity behind.
[TestFixture]
[TestOf(typeof(FSResearchSystem))]
public sealed class ResearchStateReadOnlyTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private const string RndServer = "ResearchAndDevelopmentServer";
    private const string Console = "FSResearchComputer";

    [Test]
    public async Task AttachingCreatesNoState()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var minds = entMan.System<SharedMindSystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            DeleteState(entMan);

            var body = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var mind = minds.CreateMind(Pair.Player!.UserId, "test");
            minds.TransferTo(mind, body);

            Assert.That(entMan.Count<FSStationResearchComponent>(), Is.Zero, "attaching to a body created the research state");
        });
    }

    [Test]
    public async Task LinkingAConsoleCreatesNoState()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            DeleteState(entMan);

            var rnd = entMan.SpawnEntity(RndServer, map.GridCoords);
            var console = entMan.SpawnEntity(Console, map.GridCoords);
            entMan.System<ResearchSystem>().RegisterClient(console, rnd);
            entMan.DeleteEntity(rnd);

            Assert.That(entMan.Count<FSStationResearchComponent>(), Is.Zero, "linking a console created the research state");
        });
    }

    private static void DeleteState(IEntityManager entMan)
    {
        var states = new List<EntityUid>();
        var query = entMan.AllEntityQueryEnumerator<FSStationResearchComponent>();
        while (query.MoveNext(out var uid, out _))
            states.Add(uid);

        foreach (var uid in states)
            entMan.DeleteEntity(uid);
    }
}
