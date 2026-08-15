// The unlocked-node index backs every weapon effect probe, so it must track the list exactly.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Research;
using Content.Shared._FinalStand.Research.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ResearchUnlockIndexTest : GameTest
{
    [Test]
    public async Task IndexAgreesWithTheUnlockedListThroughEveryTransition()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var research = entityManager.System<FSResearchSystem>();
            var station = research.GetOrCreateStation();

            var sample = prototypes.EnumeratePrototypes<FSTechNodePrototype>().Take(3).ToList();
            Assert.That(sample, Is.Not.Empty, "no research nodes to test against");

            foreach (var node in sample)
            {
                Assert.That(research.IsNodeUnlocked(node.ID), Is.False,
                    $"{node.ID} reported unlocked before anything was researched");
            }

            var unlockedCount = research.UnlockAllNodes();
            Assert.That(unlockedCount, Is.GreaterThan(0));

            foreach (var node in sample)
            {
                Assert.That(research.IsNodeUnlocked(node.ID), Is.True,
                    $"{node.ID} is in UnlockedNodes but the index disagrees");
            }

            Assert.That(station.Comp.UnlockedLookup, Has.Count.EqualTo(station.Comp.UnlockedNodes.Count),
                "index and list have drifted apart");

            var unknown = "FSTechNodeThatDoesNotExist";
            Assert.That(research.IsNodeUnlocked(unknown), Is.False,
                "index reported an unknown node as unlocked");
        });
    }
}
