using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Research;
using Content.Shared._FinalStand.Research.Components;
using Content.Shared.Research.Components;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ResearchTeardownTest : GameTest
{
    private static readonly EntProtoId ServerProto = "ResearchAndDevelopmentServer";
    private static readonly EntProtoId ConsoleProto = "FSResearchComputer";
    private static readonly EntProtoId MedicalConsoleProto = "FSMedicalResearchComputer";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    [Test]
    public async Task DeletingEverythingLeavesNoResearchStateBehind()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            SEntMan.SpawnEntity(ServerProto, map.GridCoords);
            SEntMan.SpawnEntity(ConsoleProto, map.GridCoords);
            SEntMan.SpawnEntity(ConsoleProto, map.GridCoords);
            SEntMan.SpawnEntity(MedicalConsoleProto, map.GridCoords);
        });
        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<FSResearchSystem>().TryGetStation(out var station), Is.True);
            Assert.That(SEntMan.HasComponent<ResearchServerComponent>(station.Owner), Is.False,
                "test precondition: the player joined before the server existed, so the research state lives on its own entity");
        });

        await Server.WaitPost(() => Server.ResolveDependency<IConsoleHost>().ExecuteCommand("entities delete"));
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityCount, Is.Zero,
                "leftover: " + string.Join(", ", SEntMan.GetEntities().Select(e => SEntMan.ToPrettyString(e))));
        });
    }
}
