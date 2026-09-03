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

    // Pellets spawn inside the shooter's own tile, so without an arming window they deflect off
    // whatever is next to the muzzle and leave in two 45 degree fans instead of going forwards.
    [DataField]
    public TimeSpan ArmDelay = TimeSpan.FromSeconds(0.15);

    public TimeSpan NextBounce;
}
