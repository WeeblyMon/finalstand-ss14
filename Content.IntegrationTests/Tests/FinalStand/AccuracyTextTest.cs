using System.Globalization;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

// Accuracy upgrades quote how far a level cuts max spread. Most quoted their raw value, five times the real cut, so
// each description is checked against the formula the upgrade applies. On a shotgun a level also narrows the pellet
// cone, which is most of what it does there, so those say so too.
[TestFixture]
[TestOf(typeof(WeaponUpgradeDef))]
public sealed class AccuracyTextTest : GameTest
{
    private static readonly ProtoId<TagPrototype> ShotgunTag = "WeaponGunShotgun";
    private static readonly string PelletCut =
        (WeaponUpgradeDef.PelletSpreadCutPerLevel * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%";

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

                    var shotgun = false;
                    if (shop.WeaponProtoId is { } weaponId
                        && protos.Index(weaponId).TryGetComponent<TagComponent>(out var tags, factory))
                    {
                        var tagSet = tags.Tags;
                        shotgun = tagSet.Contains(ShotgunTag);
                    }

                    foreach (var def in shop.Upgrades)
                    {
                        if (def.Type is not (WeaponUpgradeType.Accuracy or WeaponUpgradeType.AngleMax)
                            || def.DescriptionKey is not { } key)
                            continue;

                        checkedAny = true;
                        var text = loc.GetString(key);
                        var cut = def.AngleDeltaPerLevel().Max.ToString("0.##", CultureInfo.InvariantCulture) + "°";
                        Assert.That(text, Does.Contain(cut), $"{def.Id} should say it cuts max spread by {cut} a level");

                        if (shotgun)
                            Assert.That(text, Does.Contain(PelletCut), $"{def.Id} should say it narrows the pellet cone by {PelletCut} a level");
                    }
                }
            });

            Assert.That(checkedAny, "found no accuracy upgrades to check");
        });
    }
}
