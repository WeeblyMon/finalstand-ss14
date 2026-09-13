using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class MedicalResearchContentTest : GameTest
{
    private const string MedicalBranch = "Medical";

    // A node whose technology unlocks a recipe that sits in no lathe pack spends the department's
    // money and hands back nothing. That has already shipped once.
    [Test]
    public async Task EveryUnlockedRecipeReachesALathe()
    {
        var server = Pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var packed = new HashSet<string>();
            foreach (var pack in protoMan.EnumeratePrototypes<LatheRecipePackPrototype>())
            {
                foreach (var recipe in pack.Recipes)
                    packed.Add(recipe.Id);
            }

            var missing = new List<string>();
            var checkedAny = false;

            foreach (var node in MedicalNodes(protoMan))
            {
                if (node.VanillaTechnologyId is not { } techId)
                    continue;

                Assert.That(protoMan.TryIndex<TechnologyPrototype>(techId, out var tech), Is.True,
                    $"Medical node '{node.ID}' names technology '{techId}', which does not exist.");

                foreach (var recipe in tech!.RecipeUnlocks)
                {
                    checkedAny = true;
                    if (!packed.Contains(recipe.Id))
                        missing.Add($"{node.ID} -> {techId} -> {recipe.Id}");
                }
            }

            Assert.That(checkedAny, Is.True, "No medical node unlocks any recipe - this guard is not testing anything.");
            Assert.That(missing, Is.Empty,
                "Medical research unlocks recipes that are in no lathe pack, so buying the node does nothing:\n"
                + string.Join("\n", missing));
        });
    }

    // Unlocked("...") on a string that matches no prototype is silently false forever.
    [Test]
    public async Task EveryNodeIdReadByCodeExists()
    {
        var server = Pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var nodeId in FSMedicalUpgradeSystem.AllNodes)
            {
                Assert.That(protoMan.HasIndex<FSTechNodePrototype>(nodeId), Is.True,
                    $"FSMedicalUpgradeSystem reads node '{nodeId}', which is not a real node - that check can never be true.");
            }
        });
    }

    [Test]
    public async Task EveryMedicalNodeIsLegible()
    {
        var server = Pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var nodes = MedicalNodes(protoMan).ToArray();
            Assert.That(nodes, Has.Length.GreaterThanOrEqualTo(25), "The medical tree lost its authored nodes.");

            foreach (var node in nodes)
            {
                // The description is the only thing the buyer reads before spending the fund.
                Assert.That(node.BonusDescription, Is.Not.Empty,
                    $"Medical node '{node.ID}' has no bonusDescription, so the console shows an unexplained price.");

                foreach (var prereq in node.Prerequisites)
                {
                    Assert.That(protoMan.HasIndex<FSTechNodePrototype>(prereq), Is.True,
                        $"Medical node '{node.ID}' requires '{prereq}', which does not exist.");
                }

                foreach (var group in node.PrerequisiteGroups)
                {
                    foreach (var prereq in group)
                    {
                        Assert.That(protoMan.HasIndex<FSTechNodePrototype>(prereq), Is.True,
                            $"Medical node '{node.ID}' lists '{prereq}' in a prerequisite group, which does not exist.");
                    }
                }
            }
        });
    }

    // An exclusive group with only one member silently locks nothing.
    [Test]
    public async Task ExclusiveGroupsHaveTwoSides()
    {
        var server = Pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var groups = new Dictionary<string, List<string>>();

            foreach (var node in MedicalNodes(protoMan))
            {
                if (node.ExclusiveGroup is not { } group)
                    continue;

                if (!groups.TryGetValue(group, out var members))
                    groups[group] = members = new List<string>();

                members.Add(node.ID);
            }

            Assert.That(groups, Is.Not.Empty, "No exclusive medical choices exist.");

            foreach (var (group, members) in groups)
            {
                Assert.That(members, Has.Count.GreaterThanOrEqualTo(2),
                    $"Exclusive group '{group}' has only {members.Count} member(s), so it excludes nothing.");
            }
        });
    }

    private static IEnumerable<FSTechNodePrototype> MedicalNodes(IPrototypeManager protoMan)
    {
        return protoMan.EnumeratePrototypes<FSTechNodePrototype>()
            .Where(n => n.Branch == MedicalBranch);
    }
}
