using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Shop;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

// Upgrade values are applied as written, so a value in the wrong unit silently breaks the upgrade: a fraction in an
// integer field truncates to nothing, a percentage written as a whole number heals many times the damage dealt.
[TestFixture]
public sealed class WeaponUpgradeValuesTest : GameTest
{
    // Applied with an (int) cast.
    private static readonly HashSet<WeaponUpgradeType> WholeNumberTypes =
    [
        WeaponUpgradeType.MoneyGainBonus,
        WeaponUpgradeType.MoneyPerHit,
        WeaponUpgradeType.Radius,
        WeaponUpgradeType.MagazineSize,
        WeaponUpgradeType.PelletCount,
        WeaponUpgradeType.FlintlockCritSynergy,
        WeaponUpgradeType.GrenadeCapacity,
        WeaponUpgradeType.GrenadeRegen,
        WeaponUpgradeType.DeployableCapacity,
        WeaponUpgradeType.DeployableRegen,
        WeaponUpgradeType.AmmoBoxUses,
        WeaponUpgradeType.SentryAmmo,
        WeaponUpgradeType.LandmineDetonations,
    ];

    // A share of something, so at most all of it once fully upgraded.
    private static readonly HashSet<WeaponUpgradeType> FractionTypes =
    [
        WeaponUpgradeType.MoneyGainPercent,
    ];

    // A share per level that may stack past the whole (Blood Thirster heals 170% at level 2), but one level is never
    // more than all of it.
    private static readonly HashSet<WeaponUpgradeType> PerLevelFractionTypes =
    [
        WeaponUpgradeType.LifeSteal,
    ];

    // Read as the effect's per-level strength; zero would sell an upgrade that does nothing.
    private static readonly HashSet<WeaponUpgradeType> MagnitudeTypes =
    [
        WeaponUpgradeType.SetOnFire,
        WeaponUpgradeType.StaminaSteal,
        WeaponUpgradeType.Bleed,
    ];

    [Test]
    public async Task UpgradeValuesAreInTheirTypesUnits()
    {
        var server = Pair.Server;
        var protos = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            var checkedAny = false;
            Assert.Multiple(() =>
            {
                foreach (var proto in protos.EnumeratePrototypes<EntityPrototype>())
                {
                    if (proto.Abstract || !proto.TryGetComponent<FSShopWeaponComponent>(out var shop, factory))
                        continue;

                    foreach (var def in shop.Upgrades)
                    {
                        checkedAny = true;
                        var name = $"{proto.ID}/{def.Id}";

                        if (WholeNumberTypes.Contains(def.Type))
                            Assert.That(def.ValuePerLevel % 1f, Is.Zero, $"{name}: {def.Type} truncates {def.ValuePerLevel} to a whole number");

                        if (FractionTypes.Contains(def.Type))
                        {
                            Assert.That(def.ValuePerLevel * def.MaxLevel, Is.InRange(0f, 1f),
                                $"{name}: {def.Type} is a share, {def.ValuePerLevel} x {def.MaxLevel} levels");
                        }

                        if (PerLevelFractionTypes.Contains(def.Type))
                        {
                            Assert.That(def.ValuePerLevel, Is.InRange(0f, 1f),
                                $"{name}: {def.Type} is a share per level, got {def.ValuePerLevel}");
                        }

                        if (MagnitudeTypes.Contains(def.Type))
                            Assert.That(def.ValuePerLevel, Is.GreaterThan(0f), $"{name}: {def.Type} needs a strength per level");
                    }
                }
            });

            Assert.That(checkedAny, "found no weapon upgrades to check");
        });
    }
}
