using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Medical;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class SurgicalHealingTest : GameTest
{
    private const string BluntDamage = "Blunt";

    private const string HumanProto = "MobHuman";

    [Test]
    public async Task HealingTheBodyDoesNotMakeThingsWorse()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var damageable = entMan.System<DamageableSystem>();
            var lookup = entMan.System<OrganLookupSystem>();

            var mob = entMan.SpawnEntity(HumanProto, map.GridCoords);
            var body = entMan.GetComponent<BodyComponent>(mob);
            var brute = new DamageSpecifier(protos.Index<DamageTypePrototype>(BluntDamage), 1);

            damageable.TryChangeDamage(mob, brute * 60f, ignoreResistances: true);

            var damageBefore = (float) damageable.GetTotalDamage(mob);
            var severityBefore = TotalSeverity(entMan, lookup, mob, body);

            Assert.That(damageBefore, Is.GreaterThan(0f), "setup failed - the mob took no damage");

            damageable.TryChangeDamage(mob,
                brute * -15f,
                ignoreResistances: true,
                targetPart: TargetBodyPart.Chest);

            var damageAfter = (float) damageable.GetTotalDamage(mob);
            var severityAfter = TotalSeverity(entMan, lookup, mob, body);

            Assert.Multiple(() =>
            {
                Assert.That(damageAfter, Is.LessThanOrEqualTo(damageBefore),
                    "healing the body raised its damage instead of lowering it");
                Assert.That(severityAfter, Is.LessThanOrEqualTo(severityBefore),
                    "healing the body made the limbs worse");
            });
        });
    }

    private static float TotalSeverity(IEntityManager entMan, OrganLookupSystem lookup, EntityUid mob, BodyComponent body)
    {
        var total = 0f;
        foreach (var (partId, _) in lookup.GetBodyOrgans((mob, body)))
        {
            if (entMan.TryGetComponent<WoundableComponent>(partId, out var woundable))
                total += (float) (woundable.IntegrityCap - woundable.WoundableIntegrity);
        }

        return total;
    }
}
