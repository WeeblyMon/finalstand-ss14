using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Weapons;

// hold to charge: longer holds hit harder, fly faster, pierce further and bounce more
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSChargeShotComponent : Component
{
    // Upgrade-mutated stats need AutoNetworkedField: this system's charge ramp runs shared (both
    // sides), so a client that never receives an upgrade's write just predicts against stale defaults.
    [DataField, AutoNetworkedField]
    public float MaxChargeTime = 1.2f;

    [DataField, AutoNetworkedField]
    public float MinDamageMultiplier = 0.35f;

    [DataField, AutoNetworkedField]
    public float MaxDamageMultiplier = 1.0f;

    [DataField, AutoNetworkedField]
    public int MaxBonusPierce = 3;

    [DataField, AutoNetworkedField]
    public float MinPelletScale = 0.5f;

    [DataField, AutoNetworkedField]
    public float MaxPelletScale = 2.0f;

    [DataField, AutoNetworkedField]
    public float MinSpeedMultiplier = 0.8f;

    [DataField, AutoNetworkedField]
    public float MaxSpeedMultiplier = 1.8f;

    [DataField, AutoNetworkedField]
    public int MinBounces;

    [DataField, AutoNetworkedField]
    public int MaxBounces = 3;

    [DataField, AutoNetworkedField]
    public float BounceDamageRetained = 0.8f;

    [DataField, AutoNetworkedField]
    public float BounceSpeedRetained = 0.85f;

    [DataField, AutoNetworkedField]
    public bool BounceRefund;

    [DataField, AutoNetworkedField]
    public bool BounceCrit;

    [DataField, AutoNetworkedField]
    public bool Fracture;

    [DataField]
    public EntProtoId? FragmentProto = "FSBulletLaserShotgunFragment";

    [DataField]
    public SoundSpecifier? ChargeSound =
        new SoundPathSpecifier("/Audio/_FinalStand/Weapons/EnergyShotgun/charge.ogg");

    [AutoNetworkedField]
    public float Charge;

    public TimeSpan? ChargeStart;
    public TimeSpan LastHeld;
    public EntityUid? Shooter;

    public Vector2 AimDirection;
    public EntityUid? ChargeStream;
}
