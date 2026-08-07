// The shop's 0-100 accuracy figure. Computed here because AngleIncrease is not a networked
// field, so the client cannot see what an upgrade did to it.
using Content.Shared._FinalStand.Shop;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Shop;

public sealed partial class FSShopWeaponSystem
{
    // What one level takes off each spread angle. Mirrors FSPlayerUpgradesSystem.GunStats.
    private static (double Min, double Max, double Inc) AngleDeltaPerLevel(WeaponUpgradeDef def) => def.Type switch
    {
        WeaponUpgradeType.Accuracy => (def.ValuePerLevel * 0.5, def.ValuePerLevel * 0.2, def.ValuePerLevel * 0.3),
        WeaponUpgradeType.AngleMax => (0.0, def.ValuePerLevel, 0.0),
        _                          => (0.0, 0.0, 0.0),
    };

    // One number standing for how tightly the weapon shoots. MinAngle is the aimed shot,
    // MaxAngle the worst case under sustained fire, AngleIncrease how fast it gets there.
    private static double Spread(double min, double max, double inc)
        => min * 0.5 + max * 0.3 + inc * 0.2;

    // A wieldable gun is always fired wielded, so its wield bonus is what the player experiences.
    // BaseGunWieldable is 21/32 degrees with a -20/-30 bonus: measuring the unwielded gun makes an
    // upgrade worth a degree or two look like nothing.
    private static (double Min, double Max, double Inc) Effective(GunComponent gun, GunWieldBonusComponent? wield)
    {
        var min = gun.MinAngle.Degrees;
        var max = gun.MaxAngle.Degrees;
        var inc = gun.AngleIncrease.Degrees;

        if (wield != null)
        {
            min += wield.MinAngle.Degrees;
            max += wield.MaxAngle.Degrees;
            inc += wield.AngleIncrease.Degrees;
        }

        min = Math.Max(0.0, min);
        max = Math.Max(min, max);
        inc = Math.Max(0.0, inc);
        return (min, max, inc);
    }

    private static double SpreadOf(GunComponent gun, GunWieldBonusComponent? wield)
    {
        var (min, max, inc) = Effective(gun, wield);
        return Spread(min, max, inc);
    }

    // Anchored so an unmodified weapon reads the shop's StatAccuracy and zero spread reads 100.
    private static int ToAccuracy(double spread, double baseSpread, int stat)
    {
        if (baseSpread <= 0.0)
            return 100;

        var frac = Math.Clamp(1.0 - spread / baseSpread, 0.0, 1.0);
        return (int)Math.Round(stat + frac * (100 - stat));
    }

    private bool TryGetBaseGun(FSShopWeaponComponent comp,
        out GunComponent baseGun, out GunWieldBonusComponent? baseWield)
    {
        baseGun = default!;
        baseWield = null;

        if (comp.WeaponProtoId is not { } id
            || !_protoManager.TryIndex<EntityPrototype>(id, out var proto)
            || !proto.TryGetComponent(out baseGun!, EntityManager.ComponentFactory))
            return false;

        proto.TryGetComponent(out baseWield, EntityManager.ComponentFactory);
        return true;
    }

    /// <summary>Current accuracy, and what each upgrade would give at one level higher.</summary>
    private (int Current, Dictionary<string, int> Next) ComputeAccuracy(EntityUid player, FSShopWeaponComponent comp)
    {
        var next = new Dictionary<string, int>();

        if (!TryGetBaseGun(comp, out var baseGun, out var baseWield))
            return (-1, next);

        var baseSpread = SpreadOf(baseGun, baseWield);

        var owned = _search.FindFirst(player, ShopProtoIds(comp));
        if (owned == null || !TryComp<GunComponent>(owned.Value.Uid, out var gun))
            return (comp.StatAccuracy, next);

        var wield = CompOrNull<GunWieldBonusComponent>(owned.Value.Uid);
        var current = ToAccuracy(SpreadOf(gun, wield), baseSpread, comp.StatAccuracy);

        // Replay one more level through the same clamps the upgrade uses, so the shop's preview
        // and the value after buying cannot disagree.
        foreach (var def in comp.Upgrades)
        {
            var (dMin, dMax, dInc) = AngleDeltaPerLevel(def);
            if (dMin == 0.0 && dMax == 0.0 && dInc == 0.0)
                continue;

            // The upgrade clamps against MinAngle before the wield bonus, so replay in that order.
            var rawMin = Math.Max(0.0, gun.MinAngle.Degrees - dMin);
            var rawMax = Math.Max(rawMin, gun.MaxAngle.Degrees - dMax);
            var rawInc = Math.Max(0.0, gun.AngleIncrease.Degrees - dInc);

            var min = Math.Max(0.0, rawMin + (wield?.MinAngle.Degrees ?? 0.0));
            var max = Math.Max(min, rawMax + (wield?.MaxAngle.Degrees ?? 0.0));
            var inc = Math.Max(0.0, rawInc + (wield?.AngleIncrease.Degrees ?? 0.0));
            next[def.Id] = ToAccuracy(Spread(min, max, inc), baseSpread, comp.StatAccuracy);
        }

        return (current, next);
    }
}
