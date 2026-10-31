using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Fortifications;

// Cuts and slows wave attackers that hit it, and can't be vaulted.
[RegisterComponent, NetworkedComponent]
[Access(typeof(FSBarbedWireClimbSystem))]
public sealed partial class FSBarbedWireComponent : Component
{
    [DataField]
    public float Damage = 3f;

    [DataField]
    public float Slow = 0.75f;

    [DataField]
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(1.5);
}
