using System.Collections.Generic;
using Content.Client._FinalStand.Engineering;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Engineering;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// Objectives are only sent to engineers. A player who isn't one next round gets nothing that would replace last
// round's list, so the restart has to clear it.
[TestFixture]
[TestOf(typeof(FSEngineeringObjectiveClientSystem))]
public sealed class EngineeringObjectiveRestartTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true };

    [Test]
    public async Task RestartClearsObjectives()
    {
        var sNet = Pair.Server.ResolveDependency<IEntityManager>().EntityNetManager!;
        var objectives = Pair.Client.System<FSEngineeringObjectiveClientSystem>();

        await Pair.Server.WaitPost(() =>
            sNet.SendSystemNetworkMessage(new FSEngineeringObjectiveEvent(new List<string> { "silo low" })));
        await Pair.RunTicksSync(5);

        await Pair.Client.WaitAssertion(() =>
            Assert.That(objectives.Lines, Is.EqualTo(new[] { "silo low" }), "the objectives never reached the client"));

        await Pair.Server.WaitPost(() => sNet.SendSystemNetworkMessage(new RoundRestartCleanupEvent()));
        await Pair.RunTicksSync(5);

        await Pair.Client.WaitAssertion(() =>
            Assert.That(objectives.Latest, Is.Null, "last round's objectives survived the restart"));
    }
}
