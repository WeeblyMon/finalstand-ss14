using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Upgrades.Effects;

// Scales incoming knockback and decides whether it locks the target in place.
[RegisterComponent, NetworkedComponent]
public sealed partial class FSKnockbackResistComponent : Component
{
    [DataField]
    public float Multiplier = 1f;

    [DataField]
    public bool LocksMovement = true;
}
