using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Research;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

// Two halves of an exclusive choice can be picked at once (shared by the RD, personally by a scientist);
// finishing one must drop the other instead of letting both unlock.
[TestFixture]
public sealed class ResearchExclusiveChoiceTest : GameTest
{
    // Station research is round state; a fast-recycled pair would hand these unlocks to the next test.
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private static readonly ProtoId<FSTechNodePrototype> Tandem = "FSOrdnanceTandemWarheads";
    private static readonly ProtoId<FSTechNodePrototype> Thermobaric = "FSOrdnanceThermobaricPayloads";

    [Test]
    public async Task FinishingOneHalfDropsTheOther()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var research = entMan.System<FSResearchSystem>();
            var minds = entMan.System<SharedMindSystem>();
            var station = research.GetOrCreateStation();

            Assert.That(protos.Index(Tandem).ExclusiveGroup, Is.Not.Null.And.EqualTo(protos.Index(Thermobaric).ExclusiveGroup),
                "the two test nodes should be halves of the same choice");

            var scientist = minds.CreateMind(null, "Scientist").Owner;
#pragma warning disable RA0002
            station.Comp.ActiveResearch = Tandem;
            station.Comp.PersonalPicks[scientist] = Thermobaric;
#pragma warning restore RA0002

            research.GrantResearchPoints(protos.Index(Tandem).Cost, "test");

            Assert.That(research.IsNodeUnlocked(Tandem.Id));
#pragma warning disable RA0002
            Assert.That(station.Comp.PersonalPicks.ContainsKey(scientist), Is.False,
                "the personal pick on the other half should have been dropped");
#pragma warning restore RA0002

            research.GrantResearchPoints(protos.Index(Thermobaric).Cost * 2, "test", scientist);
            Assert.That(research.IsNodeUnlocked(Thermobaric.Id), Is.False, "both halves of a choice unlocked");
        });
    }
}
