using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Fluids.Components;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class PotionFrameworkTest : GameTest
{
    private const string HumanProto = "MobHuman";
    private const string FlaskProto = "FSSplashFlask";

    [Test]
    public async Task MeleeSpeedBonusReachesTheAttackRate()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var bonus = entMan.System<FSMedicalBonusSystem>();
            var melee = entMan.System<SharedMeleeWeaponSystem>();

            var mob = entMan.SpawnEntity(HumanProto, map.GridCoords);
            var baseRate = melee.GetAttackRate(mob, mob);

            bonus.ApplyBuff(mob, "chem-stim", new Dictionary<FSMedicalBonusCategory, float>
            {
                [FSMedicalBonusCategory.MeleeSpeed] = 0.3f,
            });

            Assert.That(melee.GetAttackRate(mob, mob), Is.GreaterThan(baseRate),
                "MeleeSpeed has no consumer - it would be a dead category");
        });
    }

    [Test]
    public async Task TheStimPotionBuffsCrewAndNotZombies()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var bonus = entMan.System<FSMedicalBonusSystem>();

            var crew = entMan.SpawnEntity(HumanProto, map.GridCoords);
            entMan.EnsureComponent<FSFriendlyFireComponent>(crew);

            var zombie = entMan.SpawnEntity(HumanProto, map.GridCoords);
            entMan.RemoveComponent<FSFriendlyFireComponent>(zombie);

            var reactive = entMan.System<ReactiveSystem>();
            var potion = new Solution("FSCombatStim", 20);

            reactive.DoEntityReaction(crew, potion, ReactionMethod.Touch);
            reactive.DoEntityReaction(zombie, potion, ReactionMethod.Touch);

            Assert.Multiple(() =>
            {
                Assert.That(bonus.GetBonus(crew, FSMedicalBonusCategory.MeleeSpeed), Is.GreaterThan(0f),
                    "crew must receive the buff");
                Assert.That(bonus.GetBonus(zombie, FSMedicalBonusCategory.MeleeSpeed), Is.EqualTo(0f),
                    "a buff potion must never strengthen the horde");
            });
        });
    }

    [Test]
    public async Task TheFlaskCloudsWithoutAnySmokePrecursor()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var solutions = entMan.System<SharedSolutionContainerSystem>();

            var flask = entMan.SpawnEntity(FlaskProto, map.GridCoords);
            Assert.That(solutions.TryGetSolution(flask, "flask", out var soln, out _), Is.True);

            solutions.TryAddReagent(soln!.Value, "FSCorrosive", 30);

            var ev = new LandEvent(null, false);
            entMan.EventBus.RaiseLocalEvent(flask, ref ev);

            var clouds = entMan.EntityQuery<SmokeComponent>().Count();
            Assert.That(clouds, Is.GreaterThan(0),
                "the whole point of a bespoke vessel is that any potion clouds, precursor or not");
        });
    }

    [Test]
    public async Task DifferentPotionsUseTheSameFlask()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var solutions = entMan.System<SharedSolutionContainerSystem>();

            var before = entMan.EntityQuery<SmokeComponent>().Count();

            foreach (var reagent in new[] { "FSCorrosive", "FSCombatStim" })
            {
                var flask = entMan.SpawnEntity(FlaskProto, map.GridCoords);
                Assert.That(solutions.TryGetSolution(flask, "flask", out var soln, out _), Is.True,
                    $"{reagent} must load into the same vessel");

                solutions.TryAddReagent(soln!.Value, reagent, 20);

                var ev = new LandEvent(null, false);
                entMan.EventBus.RaiseLocalEvent(flask, ref ev);
            }

            Assert.That(entMan.EntityQuery<SmokeComponent>().Count(), Is.EqualTo(before + 2),
                "one vessel, two different payloads, two clouds - that is the framework");
        });
    }
}
