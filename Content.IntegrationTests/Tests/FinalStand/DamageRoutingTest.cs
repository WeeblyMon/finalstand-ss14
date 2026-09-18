using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Medical;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class DamageRoutingTest : GameTest
{
    private const string HumanProto = "MobHuman";

    [Test]
    public async Task UntargetedDamageSpreadsAcrossLimbs()
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
            var brute = new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), 1);

            for (var i = 0; i < 60; i++)
                damageable.TryChangeDamage(mob, brute * 3f, ignoreResistances: true);

            var wounded = 0;
            foreach (var (partId, _) in lookup.GetBodyOrgans((mob, body)))
            {
                if (entMan.TryGetComponent<WoundableComponent>(partId, out var woundable)
                    && woundable.WoundableIntegrity < woundable.IntegrityCap)
                {
                    wounded++;
                }
            }

            Assert.That(wounded, Is.GreaterThan(1),
                "every hit landed on one part - the victim's own targeting is steering incoming damage again");
        });
    }
}
