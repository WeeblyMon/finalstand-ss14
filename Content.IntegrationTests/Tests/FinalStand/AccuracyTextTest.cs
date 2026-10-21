using System.Globalization;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Shop;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

// Accuracy upgrades quote how far a level cuts max spread. Most quoted their raw value, five times the real cut, so
// each description is checked against the formula the upgrade applies.
[TestFixture]
[TestOf(typeof(WeaponUpgradeDef))]
public sealed class AccuracyTextTest : GameTest
{
    [Test]
    public async Task AccuracyDescriptionsQuoteTheRealCut()
    {
        var server = Pair.Server;
        var protos = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();
        var loc = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            var checkedAny = false;
            Assert.Multiple(() =>
            {
                foreach (var proto in protos.EnumeratePrototypes<EntityPrototype>())
                {
                    if (!proto.TryGetComponent<FSShopWeaponComponent>(out var shop, factory))
                        continue;

                    foreach (var def in shop.Upgrades)
                    {
                        if (def.Type != WeaponUpgradeType.Accuracy || def.DescriptionKey is not { } key)
                            continue;

                        checkedAny = true;
                        var cut = def.AngleDeltaPerLevel().Max.ToString("0.##", CultureInfo.InvariantCulture) + "°";
                        Assert.That(loc.GetString(key), Does.Contain(cut), $"{def.Id} should say it cuts max spread by {cut} a level");
                    }
                }
            });

            Assert.That(checkedAny, "found no accuracy upgrades to check");
        });
    }
}
