// Part of FSPlayerUpgradesSystem: gun statistics: rate of fire, spread, capacity, reload, firing mode.
using Content.Shared._FinalStand.Shop;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Power.Components;

namespace Content.Server._FinalStand.Shop;

public sealed partial class FSPlayerUpgradesSystem
{
    private bool TryApplyGunStats(EntityUid weapon, EntityUid player, WeaponUpgradeDef def,
        int newLevel, bool spawnItems, FSWeaponUpgradeStateComponent state)
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
                {
                    state.PelletSpreadMultiplier = Math.Max(0.1f, state.PelletSpreadMultiplier - 0.16f);
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
                // WieldBonus is intentionally left unchanged: scaling it alongside the base angles
                // drives MaxAngleModified negative, inverting CurrentAngle clamping when wielded.
                _gun.RefreshModifiers(weapon);
                {
                    state.PelletSpreadMultiplier = Math.Max(0.1f, state.PelletSpreadMultiplier - 0.16f);
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
                    state.BatteryFireCostReduction += def.ValuePerLevel;
                    Dirty(weapon, bat);
                    // Shots/Capacity are cached off FireCost and only recomputed on ChargeChangedEvent -
                    // force a refresh now so the ammo counter reflects the new FireCost immediately.
                    _gun.UpdateShots((weapon, bat));
                }
                else if (HasComp<ChamberMagazineAmmoProviderComponent>(weapon) || HasComp<MagazineAmmoProviderComponent>(weapon))
                {
                    state.MagazineSizeBonus += (int)def.ValuePerLevel;
                    ApplyMagSizeBonusToCurrentMag(weapon, (int)def.ValuePerLevel);
                    _gun.RefreshModifiers(weapon);
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
                    if (def.ValuePerLevel > 1f)
                    {
                        gunF.FireRate *= def.ValuePerLevel;
                        gunF.FireRateModified = gunF.FireRate;
                    }
#pragma warning restore RA0002
                    Dirty(weapon, gunF);
                }
                break;
            case WeaponUpgradeType.SpeedLoader:
            {
                state.SpeedLoaderEnabled = true;
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
            case WeaponUpgradeType.ReloadSpeed:
            {
                state.ReloadSpeedMultiplier = MathF.Max(0.1f, 1.0f - newLevel * def.ValuePerLevel);
                break;
            }
            case WeaponUpgradeType.AttackSpeed:
                if (TryComp<GunComponent>(weapon, out var burstGun))
                {
#pragma warning disable RA0002
                    burstGun.BurstFireRate += def.ValuePerLevel;
#pragma warning restore RA0002
                    Dirty(weapon, burstGun);
                }
                else
                {
                    state.AttackSpeedMultiplier += def.ValuePerLevel;
                }
                break;
            case WeaponUpgradeType.PelletCount:
            {
                state.ExtraPellets += (int)def.ValuePerLevel;
                break;
            }
            case WeaponUpgradeType.Scrapshot:
            {
                state.ScrapshotEnabled = true;
                state.ExtraPellets += 3;
                if (TryComp<GunComponent>(weapon, out var gunSc))
                {
#pragma warning disable RA0002
                    gunSc.MaxAngle = Angle.FromDegrees(gunSc.MaxAngle.Degrees + 15.0);
                    gunSc.MaxAngleModified = gunSc.MaxAngle;
#pragma warning restore RA0002
                    Dirty(weapon, gunSc);
                }
                break;
            }
            case WeaponUpgradeType.SlamFire:
            {
                if (TryComp<GunComponent>(weapon, out var gunSlam))
                {
#pragma warning disable RA0002
                    gunSlam.AvailableModes |= SelectiveFire.FullAuto;
                    gunSlam.SelectedMode = SelectiveFire.FullAuto;
                    gunSlam.FireRate *= 1.4f;
                    gunSlam.FireRateModified = gunSlam.FireRate;
#pragma warning restore RA0002
                    Dirty(weapon, gunSlam);
                }
                break;
            }
            case WeaponUpgradeType.MagEfficiency:
            {
                state.MagEfficiencyLevel = newLevel;
                break;
            }

            default:
                return false;
        }

        return true;
    }
}
