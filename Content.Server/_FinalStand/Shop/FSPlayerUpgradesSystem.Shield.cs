// Part of FSPlayerUpgradesSystem: riot shield.
using Content.Shared._FinalStand.Shop;

using Content.Shared._FinalStand.RiotShield;

namespace Content.Server._FinalStand.Shop;

public sealed partial class FSPlayerUpgradesSystem
{
    private bool TryApplyShield(EntityUid weapon, EntityUid player, WeaponUpgradeDef def,
        int newLevel, bool spawnItems, FSWeaponUpgradeStateComponent state)
    {
        switch (def.Type)
        {
            case WeaponUpgradeType.Thorns:
            {
                if (TryComp<FSRiotShieldComponent>(weapon, out var thorns))
                {
                    thorns.ThornsPercent += def.ValuePerLevel;
                    Dirty(weapon, thorns);
                }
                break;
            }
            case WeaponUpgradeType.ShieldVampire:
            {
                if (TryComp<FSRiotShieldComponent>(weapon, out var vamp))
                {
                    vamp.VampirePercent += def.ValuePerLevel;
                    Dirty(weapon, vamp);
                }
                break;
            }
            case WeaponUpgradeType.ShieldDurability:
            {
                if (TryComp<FSRiotShieldComponent>(weapon, out var dur))
                {
                    dur.DurabilityMultiplier *= 2f;
                    dur.CurrentDurability = dur.BaseDurability * dur.DurabilityMultiplier;
                    dur.IsBroken = false;
                    Dirty(weapon, dur);
                }
                break;
            }

            default:
                return false;
        }

        return true;
    }
}
