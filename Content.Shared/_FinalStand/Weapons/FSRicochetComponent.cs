using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Weapons;

// projectile deflects off walls instead of being consumed by them
[RegisterComponent, NetworkedComponent]
public sealed partial class FSRicochetComponent : Component
{
    [DataField]
    public int Bounces = 1;

    [DataField]
    public float BounceAngle = 45f;

    public TimeSpan NextBounce;
}
