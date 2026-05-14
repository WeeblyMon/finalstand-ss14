using Content.Shared.FixedPoint;

namespace Content.Shared._FinalStand.Shop;

[RegisterComponent]
public sealed partial class FSWeaponUpgradeStateComponent : Component
{
    [DataField] public float CritChance = 0f;
    [DataField] public float CritDamageMultiplier = 2f;
    [DataField] public FixedPoint2 PierceThreshold = FixedPoint2.Zero;

    /// <summary>Per-weapon-instance upgrade levels. Keyed by upgrade ID.</summary>
    [DataField] public Dictionary<string, int> Levels = new();
}
