using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ChemDebuffTest : GameTest
{
    private const string ArmouredEnemy = "FSZombieArmoured";
    private const string PlainEnemy = "FSZombieNormal";

    [Test]
    public async Task SolventStopsArmourGrowingBack()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        var enemy = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            var reactive = entMan.System<ReactiveSystem>();

            enemy = entMan.SpawnEntity(ArmouredEnemy, map.GridCoords);
            var armor = entMan.GetComponent<FSArmorComponent>(enemy);

            Assert.That(armor.MaxArmor, Is.GreaterThan(0f), "the test enemy has no armour to strip");

            armor.CurrentArmor = 0f;
            armor.RegenDelayAccumulator = 0f;

            reactive.DoEntityReaction(enemy, new Solution("FSSolvent", 20), ReactionMethod.Touch);

            // Solvent removes the ceiling rather than pausing regen, so there is nothing left to
            // grow back into. This asserted the old FSArmorSuppressedComponent and was never
            // updated when that changed.
            Assert.That(armor.MaxArmor, Is.EqualTo(0f).Within(0.01f),
                "guns already shred armour - the chemist's job is making sure it stays gone");
        });

        await Pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var armor = entMan.GetComponent<FSArmorComponent>(enemy);

            Assert.That(armor.CurrentArmor, Is.EqualTo(0f).Within(0.01f),
                "armour regenerated while suppressed - the signature play does nothing");
        });
    }

    [Test]
    public async Task WeakeningMakesEveryDamageSourceBiteHarder()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var reactive = entMan.System<ReactiveSystem>();

            var plain = entMan.SpawnEntity(PlainEnemy, map.GridCoords);
            var weakened = entMan.SpawnEntity(PlainEnemy, map.GridCoords);

            reactive.DoEntityReaction(weakened, new Solution("FSWeakeningAgent", 20), ReactionMethod.Touch);
            Assert.That(entMan.HasComponent<FSVulnerableComponent>(weakened), Is.True);

            var damage = new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), 20);

            Assert.That(Modified(entMan, weakened, damage), Is.GreaterThan(Modified(entMan, plain, damage)),
                "a weakening cloud must multiply the whole team's damage, not just the chemist's");
        });
    }

    private static float Modified(IEntityManager entMan, EntityUid target, DamageSpecifier damage)
    {
        var ev = new DamageModifyEvent(new DamageSpecifier(damage));
        entMan.EventBus.RaiseLocalEvent(target, ev);
        return ev.Damage.GetTotal().Float();
    }
}
