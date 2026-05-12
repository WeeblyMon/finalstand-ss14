using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.Shared._FinalStand.Shop;

[RegisterComponent]
public sealed partial class FSWeaponUpgradeStateComponent : Component
{
    [DataField] public float CritChance = 0f;
    [DataField] public float CritDamageMultiplier = 2f;
    [DataField] public FixedPoint2 PierceThreshold = FixedPoint2.Zero;
}
