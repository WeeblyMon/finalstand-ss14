using Content.Server._FinalStand.Upgrades;
using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server._FinalStand.Shop;

public sealed class FSWeaponUpgradeRuntimeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        // AmmoShotEvent gives spawned projectile entities; GunShotEvent.Ammo has cartridges without ProjectileComponent.
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, AmmoShotEvent>(OnAmmoShot);
    }

    private void OnAmmoShot(EntityUid uid, FSWeaponUpgradeStateComponent comp, AmmoShotEvent args)
    {
        foreach (var projUid in args.FiredProjectiles)
        {
            if (!TryComp<ProjectileComponent>(projUid, out var proj))
                continue;

            if (comp.PierceThreshold > FixedPoint2.Zero)
            {
                // SS14's built-in pierce requires overkill; ours resets ProjectileSpent per-hit.
                proj.DeleteOnCollide = false;
                var pierceComp = EnsureComp<FSPierceComponent>(projUid);
                pierceComp.RemainingPierces = (int)Math.Round(comp.PierceThreshold.Float());
            }

            if (comp.APRoundsEnabled || comp.ArmorShredMagnitude > 0f)
            {
                var flags = FinalStandDamageFlags.None;
                if (comp.APRoundsEnabled)           flags |= FinalStandDamageFlags.ArmorPenetrating;
                if (comp.ArmorShredMagnitude > 0f)  flags |= FinalStandDamageFlags.ArmorShred;
                var flagsComp = EnsureComp<FSProjectileFlagsComponent>(projUid);
                flagsComp.Flags = flags;
                flagsComp.ArmorShredMagnitude = comp.ArmorShredMagnitude;
            }
        }
    }
}
