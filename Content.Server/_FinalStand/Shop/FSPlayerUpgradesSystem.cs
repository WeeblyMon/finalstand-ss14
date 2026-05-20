using System.Numerics;
using Content.Server.Popups;
using Content.Shared._FinalStand.Akimbo;
using Content.Shared._FinalStand.Shop;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Shop;

public sealed class FSPlayerUpgradesSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;

    private static readonly ProtoId<TagPrototype> AkimboTag = "AkimboEligible";

    /// <summary>
    ///     Applies one level's delta of <paramref name="def"/> to <paramref name="weapon"/>.
    ///     Call once per upgrade purchase. For SpawnItem, set <paramref name="spawnItems"/> false
    ///     when mirroring to the akimbo partner to avoid double-spawning ammo.
    /// </summary>
    public void ApplySingleUpgrade(EntityUid weapon, EntityUid player, WeaponUpgradeDef def, int newLevel, bool spawnItems = true)
    {
        switch (def.Type)
        {
            case WeaponUpgradeType.FireRate:
                if (TryComp<GunComponent>(weapon, out var gun))
                {
#pragma warning disable RA0002
                    gun.FireRate += def.ValuePerLevel;
                    gun.FireRateModified = gun.FireRate;
#pragma warning restore RA0002
                    Dirty(weapon, gun);
                }
                break;

            case WeaponUpgradeType.AngleMax:
                if (TryComp<GunComponent>(weapon, out var gunA))
                {
                    var deg = Math.Max(0.0, gunA.MaxAngle.Degrees - def.ValuePerLevel);
#pragma warning disable RA0002
                    gunA.MaxAngle = Angle.FromDegrees(deg);
                    gunA.MaxAngleModified = gunA.MaxAngle;
#pragma warning restore RA0002
                    Dirty(weapon, gunA);
                }
                break;

            case WeaponUpgradeType.Accuracy:
                if (TryComp<GunComponent>(weapon, out var gunAcc))
                {
                    var d = def.ValuePerLevel;
#pragma warning disable RA0002
                    gunAcc.MinAngle = Angle.FromDegrees(Math.Max(0.0, gunAcc.MinAngle.Degrees - d * 0.5));
                    gunAcc.MaxAngle = Angle.FromDegrees(Math.Max(gunAcc.MinAngle.Degrees, gunAcc.MaxAngle.Degrees - d * 0.2));
                    gunAcc.AngleIncrease = Angle.FromDegrees(Math.Max(0.0, gunAcc.AngleIncrease.Degrees - d * 0.3));
                    gunAcc.MinAngleModified = gunAcc.MinAngle;
                    gunAcc.MaxAngleModified = gunAcc.MaxAngle;
                    gunAcc.AngleIncreaseModified = gunAcc.AngleIncrease;
#pragma warning restore RA0002
                    Dirty(weapon, gunAcc);
                }
                break;

            case WeaponUpgradeType.SpawnItem:
                if (spawnItems && def.SpawnProtoId.HasValue)
                {
                    // Offset from player so items don't spawn inside their collider.
                    var coords = Transform(player).Coordinates.Offset(new Vector2(0.5f, 0.5f));
                    for (var i = 0; i < def.SpawnCountPerLevel; i++)
                        Spawn(def.SpawnProtoId.Value, coords);
                }
                break;

            case WeaponUpgradeType.MagazineSize:
                if (TryComp<BallisticAmmoProviderComponent>(weapon, out var bal))
                {
                    var extra = (int)def.ValuePerLevel;
#pragma warning disable RA0002
                    bal.Capacity += extra;
                    bal.UnspawnedCount = Math.Min(bal.UnspawnedCount + extra, bal.Capacity);
#pragma warning restore RA0002
                    Dirty(weapon, bal);
                    _gun.RefreshModifiers(weapon);
                }
                else if (TryComp<BatteryAmmoProviderComponent>(weapon, out var bat))
                {
#pragma warning disable RA0002
                    bat.FireCost = Math.Max(1f, bat.FireCost - def.ValuePerLevel);
#pragma warning restore RA0002
                    Dirty(weapon, bat);
                }
                break;

            case WeaponUpgradeType.Range:
                if (TryComp<GunComponent>(weapon, out var gunR))
                {
#pragma warning disable RA0002
                    gunR.ProjectileSpeed += def.ValuePerLevel;
                    gunR.ProjectileSpeedModified = gunR.ProjectileSpeed;
#pragma warning restore RA0002
                    Dirty(weapon, gunR);
                }
                break;

            case WeaponUpgradeType.FullAuto:
                if (newLevel >= 1 && TryComp<GunComponent>(weapon, out var gunF))
                {
#pragma warning disable RA0002
                    gunF.AvailableModes |= SelectiveFire.FullAuto;
                    gunF.SelectedMode = SelectiveFire.FullAuto;
#pragma warning restore RA0002
                    Dirty(weapon, gunF);
                }
                break;

            case WeaponUpgradeType.CritChance:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.CritChance = Math.Min(state.CritChance + def.ValuePerLevel, 1f);
                break;
            }

            case WeaponUpgradeType.CritDamage:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.CritDamageMultiplier += def.ValuePerLevel;
                break;
            }

            case WeaponUpgradeType.Pierce:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.PierceThreshold += FixedPoint2.New(def.ValuePerLevel);
                break;
            }

            case WeaponUpgradeType.Akimbo:
                if (newLevel == 1)
                    TryApplyAkimbo(weapon, player);
                break;

            case WeaponUpgradeType.ExplosiveShot:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.ExplosiveShotLevel = newLevel;
                break;
            }

            case WeaponUpgradeType.MoneyGainBonus:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.MoneyGainBonusPerKill += (int)def.ValuePerLevel;
                break;
            }

            case WeaponUpgradeType.Slowing:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.SlowingEnabled = true;
                break;
            }

            case WeaponUpgradeType.BeamChaining:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.BeamChainTargets = newLevel;
                break;
            }

            case WeaponUpgradeType.Knockback:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.KnockbackLevel = newLevel;
                break;
            }

            case WeaponUpgradeType.SelfChargeSpeed:
                if (TryComp<BatterySelfRechargerComponent>(weapon, out var selfCharge))
                {
                    selfCharge.AutoRechargeRate *= (float)def.ValuePerLevel;
                    Dirty(weapon, selfCharge);
                    _battery.RefreshChargeRate((weapon, null));
                }
                break;

            case WeaponUpgradeType.SetOnFire:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.SetOnFireEnabled = true;
                break;
            }

            case WeaponUpgradeType.APRounds:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.APRoundsEnabled = true;
                break;
            }

            case WeaponUpgradeType.ArmorShred:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.ArmorShredEnabled = true;
                break;
            }

            case WeaponUpgradeType.Recoil:
                // TODO(finalstand): implement when DynamicAimingCursor ticket is complete
                break;

            case WeaponUpgradeType.ReloadSpeed:
            case WeaponUpgradeType.Radius:
                break;
        }
    }

    public void TryApplyAkimbo(EntityUid gun, EntityUid player)
    {
        if (!_tags.HasTag(gun, AkimboTag))
            return;
        if (HasComp<FSAkimboGunComponent>(gun))
            return;

        var proto = MetaData(gun).EntityPrototype;
        if (proto == null)
            return;

        var newGun = Spawn(proto.ID, Transform(player).Coordinates);

        // Link pair BEFORE pickup so the Akimbo guard blocks recursion on the second gun.
        var compA = EnsureComp<FSAkimboGunComponent>(gun);
        var compB = EnsureComp<FSAkimboGunComponent>(newGun);
        compA.PairedGun = newGun;
        compB.PairedGun = gun;

        if (!_hands.TryPickupAnyHand(player, newGun))
        {
            QueueDel(newGun);
            RemComp<FSAkimboGunComponent>(gun);
            _popup.PopupEntity("No free hand for akimbo.", gun, player);
            return;
        }

        compA.MyHand = FindHandContaining(player, gun);
        compA.PairedHand = FindHandContaining(player, newGun);
        compB.MyHand = compA.PairedHand;
        compB.PairedHand = compA.MyHand;

        // Enable FullAuto on both guns so holding fire works.
        // Alternation on hold fires whichever gun is the active hand; alternation on individual
        // clicks works correctly via hand-switch.
        foreach (var g in new[] { gun, newGun })
        {
            if (!TryComp<GunComponent>(g, out var gunComp))
                continue;
#pragma warning disable RA0002
            gunComp.AvailableModes |= SelectiveFire.FullAuto;
            gunComp.SelectedMode   = SelectiveFire.FullAuto;
#pragma warning restore RA0002
            Dirty(g, gunComp);
        }

        RemComp<GunRequiresWieldComponent>(gun);
        RemComp<GunRequiresWieldComponent>(newGun);

        _gun.RefreshModifiers(gun);
        _gun.RefreshModifiers(newGun);
    }

    private string? FindHandContaining(EntityUid player, EntityUid item)
    {
        return _hands.IsHolding(player, item, out var handName) ? handName : null;
    }
}
