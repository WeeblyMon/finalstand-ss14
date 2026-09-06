using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Weapons;

// hold to charge: longer holds hit harder, fly faster, pierce further and bounce more
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSChargeShotComponent : Component
{
    [DataField]
    public float MaxChargeTime = 1.5f;

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
    public int MinBounces = 1;

    [DataField]
    public int MaxBounces = 4;

    [AutoNetworkedField]
    public float Charge;

    public TimeSpan? ChargeStart;
    public TimeSpan LastHeld;
    public EntityUid? Shooter;

    public Vector2 AimDirection;
}
