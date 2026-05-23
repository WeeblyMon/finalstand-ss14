using Content.Shared._FinalStand.Armor;

namespace Content.Shared._FinalStand.Upgrades.Effects;

[RegisterComponent]
public sealed partial class FSProjectileFlagsComponent : Component
{
    [DataField] public FinalStandDamageFlags Flags;
}
