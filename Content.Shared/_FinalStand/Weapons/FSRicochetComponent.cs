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
    public SoundSpecifier? BounceSound = new SoundPathSpecifier("/Audio/_FinalStand/Weapons/EnergyShotgun/bounce.ogg");

    [DataField]
    public EntProtoId? BounceEffect;

    [DataField]
    public bool Refund;

    [DataField]
    public float RefundFraction = 0.25f;

    [DataField]
    public bool Crit;

    [DataField]
    public float CritMultiplier = 2f;

    [DataField]
    public bool Fracture;

    [DataField]
    public int FragmentCount = 2;

    [DataField]
    public float FragmentDamage = 0.5f;

    [DataField]
    public float FragmentLifetime = 0.6f;

    [DataField]
    public EntProtoId? FragmentProto;

    public readonly HashSet<EntityUid> Hit = new();

    public TimeSpan NextBounce;
}
