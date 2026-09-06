using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Weapons;

// projectile mirrors off walls instead of being consumed by them, losing energy each time
[RegisterComponent, NetworkedComponent]
public sealed partial class FSRicochetComponent : Component
{
    [DataField]
    public int Bounces = 1;

    [DataField]
    public float SpeedRetained = 0.85f;

    [DataField]
    public float DamageRetained = 0.8f;

    [DataField]
    public float Clearance = 0.25f;

    [DataField]
    public SoundSpecifier? BounceSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Hits/energy_metal1.ogg");

    [DataField]
    public EntProtoId? BounceEffect;

    public TimeSpan NextBounce;
}
