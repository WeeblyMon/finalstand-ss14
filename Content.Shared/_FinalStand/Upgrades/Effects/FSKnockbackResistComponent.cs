// Scales incoming knockback and decides whether it locks the target in place.
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Upgrades.Effects;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSKnockbackResistComponent : Component
{
    [DataField]
    public float Multiplier = 1f;

    [DataField]
    public bool LocksMovement = true;
}
