using Content.Shared._FinalStand.Shop;

namespace Content.Shared._FinalStand.Crit;

public static class FSCritMath
{
    public const float DefaultMultiplier = 2f;

    // A weapon's crit multiplier: its own base, plus what its shop upgrades added. The shop preview uses this too.
    public static float Multiplier(CritComponent? crit, FSWeaponUpgradeStateComponent? state)
    {
        return (crit?.CritMultiplier ?? DefaultMultiplier) + (state?.CritDamageBonus ?? 0f);
    }
}
