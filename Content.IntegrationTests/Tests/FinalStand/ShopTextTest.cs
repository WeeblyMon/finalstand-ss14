using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Armor.Shop;
using Content.Shared._FinalStand.Bags;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared._FinalStand.Shop;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ShopTextTest : GameTest
{
    // The info panel has a fixed width.
    private const int MaxDescriptionLength = 90;

    [Test]
    public async Task EveryPerkHasItsText()
    {
        var server = Pair.Server;
        var loc = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var def in FSPerkDef.All.Values)
                {
                    Assert.That(loc.HasString(def.NameKey), $"{def.Id}: missing {def.NameKey}");
                    Assert.That(loc.HasString(def.DescriptionKey), $"{def.Id}: missing {def.DescriptionKey}");
                    Assert.That(def.Description, Has.Length.LessThanOrEqualTo(MaxDescriptionLength), def.Id);

                    foreach (var key in def.LevelKeys())
                        Assert.That(loc.HasString(key), $"{def.Id}: missing {key}");

                    for (var i = 0; i < FSPerkDef.MaxLevel; i++)
                        Assert.That(def.LevelEffect(i), Does.Not.Contain("{").And.Not.Contain("$"), $"{def.Id} level {i + 1}");
                }
            });
        });
    }

    [Test]
    public async Task EveryArmorTierHasAName()
    {
        var server = Pair.Server;
        var loc = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var tier in FSArmorShopDefs.Tiers)
                    Assert.That(loc.HasString(tier.NameKey), $"missing {tier.NameKey}");
            });
        });
    }

    [Test]
    public async Task ResearchAndShopEntriesResolve()
    {
        var server = Pair.Server;
        var loc = server.ResolveDependency<ILocalizationManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var node in protos.EnumeratePrototypes<FSTechNodePrototype>())
                {
                    Assert.That(loc.HasString(node.NameKey), $"{node.ID}: missing {node.NameKey}");
                    if (node.BonusDescriptionKey is { } bonus)
                        Assert.That(loc.HasString(bonus), $"{node.ID}: missing {bonus}");
                }

                foreach (var branch in protos.EnumeratePrototypes<FSTechBranchPrototype>())
                    Assert.That(loc.HasString(branch.NameKey), $"{branch.ID}: missing {branch.NameKey}");

                foreach (var proto in protos.EnumeratePrototypes<EntityPrototype>())
                {
                    if (proto.TryGetComponent<FSShopWeaponComponent>(out var shop, factory))
                    {
                        foreach (var upgrade in shop.Upgrades)
                        {
                            foreach (var key in new[] { upgrade.NameKey, upgrade.DescriptionKey, upgrade.AltNameKey, upgrade.AltDescriptionKey })
                            {
                                if (key is { } id)
                                    Assert.That(loc.HasString(id), $"{proto.ID}/{upgrade.Id}: missing {id}");
                            }
                        }
                    }

                    if (proto.TryGetComponent<FSBagShopComponent>(out var bags, factory))
                    {
                        foreach (var entry in bags.Catalog)
                        {
                            Assert.That(loc.HasString(entry.Name), $"{proto.ID}: missing {entry.Name}");
                            if (entry.Description is { } desc)
                                Assert.That(loc.HasString(desc), $"{proto.ID}: missing {desc}");
                        }
                    }
                }
            });
        });
    }
}
