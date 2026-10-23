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
            SEntMan.System<FSResearchSystem>().GetOrCreateStation();
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
                "test precondition: research started before the server existed, so the state lives on its own entity");
        });

        await Server.WaitPost(() => Server.ResolveDependency<IConsoleHost>().ExecuteCommand("entities delete"));
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityCount, Is.Zero,
                "leftover: " + string.Join(", ", SEntMan.GetEntities().Select(e => SEntMan.ToPrettyString(e))));
        });
    }

    [Test]
    public async Task GhostingAPlayerDoesNotConjureResearchState()
    {
        await Server.WaitPost(() =>
        {
            var stations = SEntMan.EntityQuery<FSStationResearchComponent>(true).Select(c => c.Owner).ToList();
            foreach (var station in stations)
                SEntMan.DeleteEntity(station);
        });
        await Pair.RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<FSResearchSystem>().TryGetStation(out _), Is.False, "test precondition");
            Assert.That(ServerSession!.AttachedEntity, Is.Not.Null, "test precondition");
            SEntMan.DeleteEntity(ServerSession!.AttachedEntity!.Value);
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(ServerSession!.AttachedEntity, Is.Not.Null, "the player was moved into a ghost");
            Assert.That(SEntMan.EntityQuery<FSStationResearchComponent>(true), Is.Empty,
                "attaching a player only reads research state, it never creates it");
        });
    }
}
