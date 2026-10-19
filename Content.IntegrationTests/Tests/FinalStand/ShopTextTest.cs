using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Armor.Shop;
using Content.Shared._FinalStand.Perks;
using Robust.Shared.Localization;

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
}
