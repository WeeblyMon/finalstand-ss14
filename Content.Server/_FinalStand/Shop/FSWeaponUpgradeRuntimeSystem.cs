using Content.Server._FinalStand.Upgrades;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server._FinalStand.Shop;

public sealed partial class FSWeaponUpgradeRuntimeSystem : EntitySystem
{
    [Dependency] private OverchargeShotUpgradeSystem _overcharge = default!;
    [Dependency] private PelletCountUpgradeSystem _pelletCount = default!;
    [Dependency] private FlechetteRoundsUpgradeSystem _flechette = default!;
    [Dependency] private MagEfficiencyUpgradeSystem _magEfficiency = default!;
    [Dependency] private OverloadRoundUpgradeSystem _overloadRound = default!;
    [Dependency] private MultishotUpgradeSystem _multishot = default!;
    [Dependency] private FSPierceSystem _pierce = default!;
    [Dependency] private FSHomingProjectileSystem _homing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, GunGetAmmoSpreadEvent>(OnGetAmmoSpread);
    }

    private void OnAmmoShot(EntityUid uid, FSWeaponUpgradeStateComponent comp, AmmoShotEvent args)
    {
        foreach (var projUid in args.FiredProjectiles)
        {
            if (!TryComp<ProjectileComponent>(projUid, out var proj))
                continue;

            if (comp.PierceThreshold > FixedPoint2.Zero)
                _pierce.GrantPierces(projUid, (int) Math.Round(comp.PierceThreshold.Float()), proj);

            if (comp.APRoundsEnabled || comp.ArmorShredMagnitude > 0f)
            {
                var flags = FinalStandDamageFlags.None;
                if (comp.APRoundsEnabled)           flags |= FinalStandDamageFlags.ArmorPenetrating;
                if (comp.ArmorShredMagnitude > 0f)  flags |= FinalStandDamageFlags.ArmorShred;
                var flagsComp = EnsureComp<FSProjectileFlagsComponent>(projUid);
                flagsComp.Flags = flags;
                flagsComp.ArmorShredMagnitude = comp.ArmorShredMagnitude;
            }

            if (comp.FlechetteEnabled)
                _pierce.GrantPierces(projUid, 1, proj);

            if (comp.DamageMultiplier > 1.0f)
                proj.Damage = proj.Damage * FixedPoint2.New(comp.DamageMultiplier);

            if (comp.HomingLevel > 0)
            {
                _homing.Home(projUid, comp.HomingLevel switch
                {
                    1 => 90f,
                    2 => 180f,
                    _ => 320f,
                });
            }
        }

        var overchargeFired = _overcharge.HandleAmmoShot(uid, comp, args);

        if (!overchargeFired)
        {
            _pelletCount.HandleAmmoShot(uid, comp, args);
            _flechette.HandleAmmoShot(uid, comp, args);
        }

        _magEfficiency.HandleAmmoShot(uid, comp, args);
        _multishot.HandleAmmoShot(uid, comp, args);
        _overloadRound.HandleAmmoShot(uid, comp, args);
    }

    private void OnGetAmmoSpread(EntityUid uid, FSWeaponUpgradeStateComponent comp, ref GunGetAmmoSpreadEvent args)
    {
        if (comp.PelletSpreadMultiplier < 1.0f)
            args.Spread *= comp.PelletSpreadMultiplier;
    }
}
