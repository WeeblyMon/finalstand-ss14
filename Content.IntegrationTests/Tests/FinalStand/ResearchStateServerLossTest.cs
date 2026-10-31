using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Research;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// The station's research belongs to the round, not to the R&D server: someone taking the machine apart (or it
// getting blown up) mustn't wipe every unlock and the banked points.
[TestFixture]
[TestOf(typeof(FSResearchSystem))]
public sealed class ResearchStateServerLossTest : GameTest
{
    // Station research is round state; a fast-recycled pair would hand these unlocks to the next test.
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private const string RndServer = "ResearchAndDevelopmentServer";

    [Test]
    public async Task ResearchSurvivesLosingTheServer()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var research = entMan.System<FSResearchSystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var rnd = entMan.SpawnEntity(RndServer, map.GridCoords);

            var before = research.GetOrCreateStation().Comp.Points;
            research.GrantResearchPoints(25, "test");
            Assert.That(research.GetOrCreateStation().Comp.Points, Is.EqualTo(before + 25), "the grant wasn't banked");

            entMan.DeleteEntity(rnd);

            Assert.That(research.TryGetStation(out var station), "the research state went with the R&D server");
            Assert.That(station.Comp.Points, Is.EqualTo(before + 25), "the banked points went with the R&D server");
        });
    }
}
