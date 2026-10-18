using System.Collections.Generic;
using System.Linq;
using Content.Shared._FinalStand.Perks;
using NUnit.Framework;

namespace Content.Tests.Shared._FinalStand;

[TestFixture]
public sealed class FSStarterBuildPlannerTest
{
    private static readonly string[] Build = { "A", "B", "C", "D", "E", "F" };

    [Test]
    public void FillsLevelOneAcrossTheBuildBeforeAnyLevelTwo()
    {
        var plan = FSStarterBuildPlanner.Make(new Dictionary<string, int>(), Build, 8);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Spent, Is.EqualTo(8));
            Assert.That(Build.Select(p => plan.Levels.GetValueOrDefault(p)), Is.EqualTo(new[] { 2, 1, 1, 1, 1, 1 }),
                "6 PP buys every level 1, the last 2 PP buy level 2 on the highest-priority perk");
        });
    }

    [Test]
    public void FifteenPointsGiveAFullBarWithFourAtLevelTwo()
    {
        var plan = FSStarterBuildPlanner.Make(new Dictionary<string, int>(), Build, 15);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Spent, Is.EqualTo(14), "a rookie's first-round 15 PP; the 15th point can't buy a level 2");
            Assert.That(Build.Select(p => plan.Levels.GetValueOrDefault(p)), Is.EqualTo(new[] { 2, 2, 2, 2, 1, 1 }));
        });
    }

    [Test]
    public void NeverSpendsMoreThanItHasOrSellsOwnedLevels()
    {
        var owned = new Dictionary<string, int> { ["A"] = 4, ["Other"] = 3 };
        var plan = FSStarterBuildPlanner.Make(owned, Build, 3);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Spent, Is.LessThanOrEqualTo(3));
            Assert.That(plan.Levels["A"], Is.EqualTo(4), "owned levels are kept as they are");
            Assert.That(plan.Levels["Other"], Is.EqualTo(3), "perks outside the build are untouched");
            Assert.That(Build.Skip(1).Take(3).Select(p => plan.Levels.GetValueOrDefault(p)), Is.EqualTo(new[] { 1, 1, 1 }));
        });
    }

    [Test]
    public void ZeroPointsBuysNothing()
    {
        var plan = FSStarterBuildPlanner.Make(new Dictionary<string, int>(), Build, 0);
        Assert.Multiple(() =>
        {
            Assert.That(plan.Spent, Is.Zero);
            Assert.That(plan.Levels, Is.Empty);
        });
    }

    [Test]
    public void SlotsOnlyHoldOwnedBuildPerksInPriorityOrder()
    {
        var levels = new Dictionary<string, int> { ["B"] = 1, ["D"] = 2, ["Other"] = 4 };
        var slots = FSStarterBuildPlanner.Slots(levels, Build);

        Assert.That(slots, Is.EqualTo(new[] { "B", "D", "", "", "", "" }));
    }
}
