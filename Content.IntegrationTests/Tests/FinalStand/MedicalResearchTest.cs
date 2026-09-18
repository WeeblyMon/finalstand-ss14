#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.MedicalOps;
using Content.Server._FinalStand.Research;
using Content.Shared._FinalStand.Research.Components;
using Content.Shared._FinalStand.Research.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class MedicalResearchTest : GameTest
{
    private const string ScienceConsole = "FSResearchComputer";
    private const string MedicalConsole = "FSMedicalResearchComputer";
    private const string MedicalBranch = "Medical";

    [Test]
    public async Task BuyingDeductsExactlyTheCostAndUnlocks()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var research = entMan.System<FSMedicalResearchSystem>();
            var fund = entMan.System<FSMedicalFundSystem>();

            var node = FirstMedicalNode(protos, research);
            Assert.That(node, Is.Not.Null, "no purchasable medical node exists");

            fund.GrantMedicalFunds(node!.Cost * 2, "test");
            var before = fund.GetBalance();

            Assert.That(research.TryPurchase(node.ID), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(research.IsNodeUnlocked(node.ID), Is.True, "node did not unlock");
                Assert.That(fund.GetBalance(), Is.EqualTo(before - node.Cost),
                    "the fund must move by exactly the node's cost");
            });
        });
    }

    [Test]
    public async Task AnEmptyFundBuysNothing()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var research = entMan.System<FSMedicalResearchSystem>();
            var fund = entMan.System<FSMedicalFundSystem>();

            var node = FirstMedicalNode(protos, research);
            Assert.That(node, Is.Not.Null);

            fund.TryDeductMedicalFunds(fund.GetBalance());
            Assert.That(fund.GetBalance(), Is.Zero);

            Assert.Multiple(() =>
            {
                Assert.That(research.TryPurchase(node!.ID), Is.False, "an empty fund must refuse");
                Assert.That(research.IsNodeUnlocked(node.ID), Is.False, "a refused buy must not unlock");
                Assert.That(fund.GetBalance(), Is.Zero, "a refused buy must not move the fund");
            });
        });
    }

    [Test]
    public async Task TheTwoTreesDoNotShareState()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var medical = entMan.System<FSMedicalResearchSystem>();
            var science = entMan.System<FSResearchSystem>();
            var fund = entMan.System<FSMedicalFundSystem>();

            var node = FirstMedicalNode(protos, medical);
            Assert.That(node, Is.Not.Null);

            var medicalConsole = entMan.SpawnEntity(MedicalConsole, map.GridCoords);
            var scienceConsole = entMan.SpawnEntity(ScienceConsole, map.GridCoords);

            fund.GrantMedicalFunds(node!.Cost, "test");
            Assert.That(medical.TryPurchase(node.ID), Is.True);

            var station = science.GetOrCreateStation();
            Assert.That(science.IsNodeUnlocked(station.Comp, node.ID), Is.False,
                "a medical purchase leaked into the science station");

            science.SyncConsoles();

            var medicalDb = entMan.GetComponent<FSTechDatabaseComponent>(medicalConsole);
            var scienceDb = entMan.GetComponent<FSTechDatabaseComponent>(scienceConsole);

            Assert.Multiple(() =>
            {
                Assert.That(medicalDb.Track, Is.EqualTo(FSResearchTrack.Medical));
                Assert.That(scienceDb.Track, Is.EqualTo(FSResearchTrack.Science));
                Assert.That(medicalDb.UnlockedNodes.Select(n => n.Id), Does.Contain(node.ID),
                    "SyncConsoles wiped the medical console's unlocks");
                Assert.That(scienceDb.UnlockedNodes.Select(n => n.Id), Does.Not.Contain(node.ID));
            });
        });
    }

    [Test]
    public async Task ConsoleBranchScopesDoNotOverlap()
    {
        var server = Pair.Server;
        var protos = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            var medical = BranchesOf(protos, factory, MedicalConsole);
            var science = BranchesOf(protos, factory, ScienceConsole);

            Assert.Multiple(() =>
            {
                Assert.That(medical, Does.Contain(MedicalBranch));
                Assert.That(science, Does.Not.Contain(MedicalBranch),
                    "medical research would show on the science console again");
                Assert.That(science, Is.Not.Empty,
                    "an empty branch list means 'show everything', which reopens the leak");
            });
        });
    }

    private static List<string> BranchesOf(IPrototypeManager protos, IComponentFactory factory, string consoleId)
    {
        var proto = protos.Index<EntityPrototype>(consoleId);
        Assert.That(proto.TryGetComponent<FSTechDatabaseComponent>(out var db, factory), Is.True,
            $"{consoleId} has no FSTechDatabase");
        return db!.Branches.Select(b => b.Id).ToList();
    }

    private static FSTechNodePrototype? FirstMedicalNode(IPrototypeManager protos, FSMedicalResearchSystem research)
    {
        return protos.EnumeratePrototypes<FSTechNodePrototype>()
            .Where(n => n.Branch == MedicalBranch && !n.Hidden)
            .Where(n => n.Prerequisites.Count == 0 && n.PrerequisiteGroups.Count == 0)
            .OrderBy(n => n.Cost)
            .FirstOrDefault(n => !research.IsNodeUnlocked(n.ID));
    }
}
