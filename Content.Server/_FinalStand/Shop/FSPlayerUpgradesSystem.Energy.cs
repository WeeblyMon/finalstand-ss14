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
            case WeaponUpgradeType.BounceCount:
            case WeaponUpgradeType.BounceRetention:
            case WeaponUpgradeType.ChargePierce:
            case WeaponUpgradeType.BounceRefund:
            case WeaponUpgradeType.BounceCrit:
            case WeaponUpgradeType.FractureRounds:
                if (TryComp<FSChargeShotComponent>(weapon, out var charge))
                    _chargeShot.TryUpgrade((weapon, charge), def.Type, def.ValuePerLevel);
                break;
            default:
                return false;
        }

        return true;
    }
}
