// The stacking maths is the whole point of the framework and nothing about it is visible to the

using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class MedicalBonusTest : GameTest
{
    private const string Dummy = "MobHuman";

    [Test]
    public async Task ASingleBuffLandsAtFullStrength()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var bonus = entMan.System<FSMedicalBonusSystem>();
            var mob = entMan.SpawnEntity(Dummy, map.GridCoords);

            bonus.ApplyBuff(mob, "a", new Dictionary<FSMedicalBonusCategory, float>
            {
                [FSMedicalBonusCategory.TreatmentSpeed] = 0.25f,
            });

            Assert.Multiple(() =>
            {
                Assert.That(bonus.GetBonus(mob, FSMedicalBonusCategory.TreatmentSpeed), Is.EqualTo(0.25f).Within(0.001f),
                    "one buff must not be diminished - the CMO gets what the tooltip promises");
                Assert.That(bonus.GetDelayMultiplier(mob, FSMedicalBonusCategory.TreatmentSpeed), Is.EqualTo(0.75f).Within(0.001f));
            });
        });
    }

    [Test]
    public async Task StackedBuffsDiminish()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var bonus = entMan.System<FSMedicalBonusSystem>();
            var mob = entMan.SpawnEntity(Dummy, map.GridCoords);

            bonus.ApplyBuff(mob, "a", new Dictionary<FSMedicalBonusCategory, float>
            {
                [FSMedicalBonusCategory.TreatmentSpeed] = 0.20f,
            });
            bonus.ApplyBuff(mob, "b", new Dictionary<FSMedicalBonusCategory, float>
            {
                [FSMedicalBonusCategory.TreatmentSpeed] = 0.10f,
            });

            Assert.That(bonus.GetBonus(mob, FSMedicalBonusCategory.TreatmentSpeed), Is.EqualTo(0.25f).Within(0.001f),
                "stacked buffs must not simply add up");
        });
    }

    [Test]
    public async Task TheStrongestBuffIsCountedFirstRegardlessOfOrder()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var bonus = entMan.System<FSMedicalBonusSystem>();
            var weakFirst = entMan.SpawnEntity(Dummy, map.GridCoords);
            var strongFirst = entMan.SpawnEntity(Dummy, map.GridCoords);

            bonus.ApplyBuff(weakFirst, "a", Single(FSMedicalBonusCategory.Movement, 0.10f));
            bonus.ApplyBuff(weakFirst, "b", Single(FSMedicalBonusCategory.Movement, 0.30f));

            bonus.ApplyBuff(strongFirst, "a", Single(FSMedicalBonusCategory.Movement, 0.30f));
            bonus.ApplyBuff(strongFirst, "b", Single(FSMedicalBonusCategory.Movement, 0.10f));

            Assert.That(bonus.GetBonus(weakFirst, FSMedicalBonusCategory.Movement),
                Is.EqualTo(bonus.GetBonus(strongFirst, FSMedicalBonusCategory.Movement)).Within(0.001f),
                "the result must not depend on which buff was applied first");
        });
    }

    [Test]
    public async Task NoAmountOfStackingBeatsTheCap()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var bonus = entMan.System<FSMedicalBonusSystem>();
            var mob = entMan.SpawnEntity(Dummy, map.GridCoords);

            for (var i = 0; i < 12; i++)
                bonus.ApplyBuff(mob, $"source{i}", Single(FSMedicalBonusCategory.TreatmentSpeed, 0.9f));

            var result = bonus.GetBonus(mob, FSMedicalBonusCategory.TreatmentSpeed);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.LessThanOrEqualTo(0.60f), "the TreatmentSpeed cap was breached");
                Assert.That(bonus.GetDelayMultiplier(mob, FSMedicalBonusCategory.TreatmentSpeed), Is.GreaterThan(0f),
                    "a delay multiplier of zero would make treatment instant");
            });
        });
    }

    [Test]
    public async Task CategoriesDoNotBleedIntoEachOther()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var bonus = entMan.System<FSMedicalBonusSystem>();
            var mob = entMan.SpawnEntity(Dummy, map.GridCoords);

            bonus.ApplyBuff(mob, "a", Single(FSMedicalBonusCategory.TreatmentSpeed, 0.25f));

            Assert.Multiple(() =>
            {
                Assert.That(bonus.GetBonus(mob, FSMedicalBonusCategory.Movement), Is.Zero);
                Assert.That(bonus.GetBonus(mob, FSMedicalBonusCategory.DefibCooldown), Is.Zero);
                Assert.That(bonus.GetInterruptionAbsorb(mob), Is.Zero);
            });
        });
    }

    [Test]
    public async Task ReapplyingTheSameSourceReplacesRatherThanStacks()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var bonus = entMan.System<FSMedicalBonusSystem>();
            var mob = entMan.SpawnEntity(Dummy, map.GridCoords);

            bonus.ApplyBuff(mob, "directive", Single(FSMedicalBonusCategory.Movement, 0.10f));
            bonus.ApplyBuff(mob, "directive", Single(FSMedicalBonusCategory.DragSpeed, 0.25f));

            Assert.Multiple(() =>
            {
                Assert.That(bonus.GetBonus(mob, FSMedicalBonusCategory.Movement), Is.Zero,
                    "the replaced directive is still applying its bonus");
                Assert.That(bonus.GetBonus(mob, FSMedicalBonusCategory.DragSpeed), Is.EqualTo(0.25f).Within(0.001f));
            });
        });
    }

    [Test]
    public async Task RemovingTheLastBuffLeavesNothingBehind()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var bonus = entMan.System<FSMedicalBonusSystem>();
            var mob = entMan.SpawnEntity(Dummy, map.GridCoords);

            bonus.ApplyBuff(mob, "a", Single(FSMedicalBonusCategory.TreatmentSpeed, 0.25f));
            bonus.RemoveBuff(mob, "a");

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<FSMedicalBonusComponent>(mob), Is.False);
                Assert.That(bonus.GetBonus(mob, FSMedicalBonusCategory.TreatmentSpeed), Is.Zero);
                Assert.That(bonus.HasBuff(mob, "a"), Is.False);
            });
        });
    }

    [Test]
    public async Task ATimedBuffStopsCountingOnceItExpires()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();
        EntityUid mob = default;

        await server.WaitAssertion(() =>
        {
            var bonus = entMan.System<FSMedicalBonusSystem>();
            mob = entMan.SpawnEntity(Dummy, map.GridCoords);

            bonus.ApplyBuff(mob, "mcp", Single(FSMedicalBonusCategory.TreatmentSpeed, 0.25f), TimeSpan.FromSeconds(0.5));
            Assert.That(bonus.GetBonus(mob, FSMedicalBonusCategory.TreatmentSpeed), Is.EqualTo(0.25f).Within(0.001f));
        });

        await Pair.RunTicksSync(60);

        await server.WaitAssertion(() =>
        {
            var bonus = entMan.System<FSMedicalBonusSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(bonus.GetBonus(mob, FSMedicalBonusCategory.TreatmentSpeed), Is.Zero,
                    "an expired buff is still applying");
                Assert.That(entMan.HasComponent<FSMedicalBonusComponent>(mob), Is.False,
                    "the component should be cleaned up once its last buff expires");
            });
        });
    }

    [Test]
    public async Task ApplyBuffCopiesTheCallersDictionary()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var bonus = entMan.System<FSMedicalBonusSystem>();
            var mob = entMan.SpawnEntity(Dummy, map.GridCoords);

            var template = Single(FSMedicalBonusCategory.TreatmentSpeed, 0.25f);
            bonus.ApplyBuff(mob, "a", template);

            template[FSMedicalBonusCategory.TreatmentSpeed] = 0.90f;

            Assert.That(bonus.GetBonus(mob, FSMedicalBonusCategory.TreatmentSpeed), Is.EqualTo(0.25f).Within(0.001f),
                "the buff aliased the caller's dictionary");
        });
    }

    [Test]
    public async Task ABuffWithNoDurationNeverExpires()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();
        EntityUid mob = default;

        await server.WaitAssertion(() =>
        {
            var bonus = entMan.System<FSMedicalBonusSystem>();
            mob = entMan.SpawnEntity(Dummy, map.GridCoords);

            bonus.ApplyBuff(mob, "directive", Single(FSMedicalBonusCategory.Movement, 0.10f));
        });

        await Pair.RunTicksSync(60);

        await server.WaitAssertion(() =>
        {
            var bonus = entMan.System<FSMedicalBonusSystem>();

            Assert.That(bonus.GetBonus(mob, FSMedicalBonusCategory.Movement), Is.EqualTo(0.10f).Within(0.001f),
                "a permanent buff was pruned");
        });
    }

    private static Dictionary<FSMedicalBonusCategory, float> Single(FSMedicalBonusCategory category, float value)
    {
        return new Dictionary<FSMedicalBonusCategory, float> { [category] = value };
    }
}
