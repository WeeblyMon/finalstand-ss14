// Healing must undo damage on the limb that took it, not be diluted across every healthy limb.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Medical;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Body;

[TestFixture]
public sealed class HealingRoutingTest : GameTest
{
    [Test]
    public async Task HealingUndoesTheDamageItMatches()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var lookup = entityManager.System<OrganLookupSystem>();
            var wounds = entityManager.System<WoundSystem>();
            var damageable = entityManager.System<DamageableSystem>();

            ProtoId<DamageTypePrototype> blunt = "Blunt";
            var bluntProto = protoManager.Index(blunt);

            var body = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);

            var before = lookup.GetBodyOrgans(body)
                .Where(o => entityManager.HasComponent<WoundableComponent>(o.Owner))
                .ToDictionary(o => o.Owner,
                    o => entityManager.GetComponent<WoundableComponent>(o.Owner).WoundableIntegrity);

            damageable.TryChangeDamage(body, new DamageSpecifier(bluntProto, 30), true);

            var hurt = before.Keys
                .Where(p => entityManager.GetComponent<WoundableComponent>(p).WoundableIntegrity < before[p])
                .ToList();

            Assert.That(hurt, Is.Not.Empty, "damaging the body wounded nothing");

            // bleeding blocks healing by design, so clear it the way gauze does
            wounds.TryHealBleedsOnBody(body, -1000f);
            damageable.TryChangeDamage(body, new DamageSpecifier(bluntProto, -30), true);

            foreach (var part in hurt)
            {
                var woundable = entityManager.GetComponent<WoundableComponent>(part);
                Assert.That(woundable.WoundableIntegrity, Is.EqualTo(before[part]),
                    $"healing did not restore {entityManager.ToPrettyString(part)} - it was diluted across healthy limbs");
            }
        });
    }
}
