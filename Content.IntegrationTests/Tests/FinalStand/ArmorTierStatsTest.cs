// The shop reads its numbers off the hardsuit, so a tier that spawns nothing usable is a silent lie.

using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Armor.Shop;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ArmorTierStatsTest : GameTest
{
    [Test]
    public async Task EveryTierResolvesToARealHardsuitWithStats()
    {
        var server = Pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            Assert.That(FSArmorShopDefs.Tiers, Is.Not.Empty);

            foreach (var tier in FSArmorShopDefs.Tiers)
            {
                Assert.That(prototypes.HasIndex<EntityPrototype>(tier.SpawnId), Is.True,
                    $"{tier.Id} spawns '{tier.SpawnId}', which is not an entity prototype");

                var stats = FSArmorShopDefs.GetStats(tier, prototypes, factory);

                Assert.That(stats.Resistances, Is.Not.Empty,
                    $"{tier.Id} shows no resistances - its hardsuit has no Armor component");

                foreach (var (damageType, reduction) in stats.Resistances)
                {
                    Assert.That(reduction, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f),
                        $"{tier.Id} advertises a {reduction * 100:0}% {damageType} reduction");
                }
            }
        });
    }

    [Test]
    public async Task NetCostRefundsHalfOfTheCurrentTier()
    {
        var server = Pair.Server;

        await server.WaitAssertion(() =>
        {
            var cheap = FSArmorShopDefs.Tiers[0];
            var dear = FSArmorShopDefs.Tiers[^1];

            Assert.That(FSArmorShopDefs.GetNetCost(null, dear), Is.EqualTo(dear.Price),
                "no current tier should mean no refund");

            Assert.That(FSArmorShopDefs.GetNetCost(cheap.Id, dear),
                Is.EqualTo(dear.Price - cheap.Price / 2),
                "upgrading should refund half of what is already owned");
        });
    }
}
