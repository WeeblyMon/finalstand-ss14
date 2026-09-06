using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Weapons;

// hold to charge: longer holds hit harder, fly faster, pierce further and bounce more
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSChargeShotComponent : Component
{
    [DataField]
    public float MaxChargeTime = 1.2f;

    [DataField]
    public float MinDamageMultiplier = 0.35f;

    [DataField]
    public float MaxDamageMultiplier = 1.0f;

    [DataField]
    public int MaxBonusPierce = 3;

    [DataField]
    public float MinPelletScale = 0.5f;

    [DataField]
    public float MaxPelletScale = 2.0f;

    [DataField]
    public float MinSpeedMultiplier = 0.8f;

    [DataField]
    public float MaxSpeedMultiplier = 1.8f;

    [DataField]
    public int MinBounces;

    [DataField]
    public int MaxBounces = 3;

    [DataField]
    public float BounceDamageRetained = 0.8f;

    [DataField]
    public float BounceSpeedRetained = 0.85f;

    [DataField]
    public bool BounceRefund;

    [DataField]
    public bool BounceCrit;

    [DataField]
    public bool Fracture;

    [DataField]
    public EntProtoId? FragmentProto = "FSBulletLaserShotgunFragment";

    [AutoNetworkedField]
    public float Charge;

    public TimeSpan? ChargeStart;
    public TimeSpan LastHeld;
    public EntityUid? Shooter;

    public Vector2 AimDirection;
}
