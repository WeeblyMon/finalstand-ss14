using Content.Shared.FixedPoint;

namespace Content.Shared._FinalStand.Shop;

[RegisterComponent]
public sealed partial class FSWeaponUpgradeStateComponent : Component
{
    [DataField] public float CritChance = 0f;
    [DataField] public float CritDamageMultiplier = 2f;
    [DataField] public FixedPoint2 PierceThreshold = FixedPoint2.Zero;

    [DataField] public Dictionary<string, int> Levels = new();

    [DataField] public int ExplosiveShotLevel = 0;
    [DataField] public int MoneyGainBonusPerKill = 0;
    [DataField] public bool SlowingEnabled = false;
    [DataField] public int BeamChainTargets = 0;
    [DataField] public int KnockbackLevel = 0;
    [DataField] public bool SetOnFireEnabled = false;
    [DataField] public bool APRoundsEnabled = false;
    [DataField] public bool ArmorShredEnabled = false;
}
