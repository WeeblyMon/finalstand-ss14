using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Weapons;

namespace Content.Server._FinalStand.Shop;

public sealed partial class FSPlayerUpgradesSystem
{
    private bool TryApplyEnergy(EntityUid weapon, EntityUid player, WeaponUpgradeDef def,
        int newLevel, bool spawnItems, FSWeaponUpgradeStateComponent state)
    {
        switch (def.Type)
        {
            case WeaponUpgradeType.ChargeSpeed:
                {
                    if (TryComp<FSChargeShotComponent>(weapon, out var charge))
                    {
                        charge.MaxChargeTime = MathF.Max(0.2f, charge.MaxChargeTime * (1f - def.ValuePerLevel));
                        Dirty(weapon, charge);
                    }
                    break;
                }
            case WeaponUpgradeType.BounceCount:
                {
                    if (TryComp<FSChargeShotComponent>(weapon, out var charge))
                    {
                        charge.MaxBounces += (int)def.ValuePerLevel;
                        Dirty(weapon, charge);
                    }
                    break;
                }
            case WeaponUpgradeType.BounceRetention:
                {
                    if (TryComp<FSChargeShotComponent>(weapon, out var charge))
                    {
                        charge.BounceDamageRetained = MathF.Min(1f, charge.BounceDamageRetained + def.ValuePerLevel);
                        charge.BounceSpeedRetained = MathF.Min(1f, charge.BounceSpeedRetained + def.ValuePerLevel);
                        Dirty(weapon, charge);
                    }
                    break;
                }
            case WeaponUpgradeType.ChargePierce:
                {
                    if (TryComp<FSChargeShotComponent>(weapon, out var charge))
                    {
                        charge.MaxBonusPierce += (int)def.ValuePerLevel;
                        Dirty(weapon, charge);
                    }
                    break;
                }
            case WeaponUpgradeType.BounceRefund:
                {
                    if (TryComp<FSChargeShotComponent>(weapon, out var charge))
                    {
                        charge.BounceRefund = true;
                        Dirty(weapon, charge);
                    }
                    break;
                }
            case WeaponUpgradeType.BounceCrit:
                {
                    if (TryComp<FSChargeShotComponent>(weapon, out var charge))
                    {
                        charge.BounceCrit = true;
                        Dirty(weapon, charge);
                    }
                    break;
                }
            case WeaponUpgradeType.FractureRounds:
                {
                    if (TryComp<FSChargeShotComponent>(weapon, out var charge))
                    {
                        charge.Fracture = true;
                        Dirty(weapon, charge);
                    }
                    break;
                }

            default:
                return false;
        }

        return true;
    }
}
