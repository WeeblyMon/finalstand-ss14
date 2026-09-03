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
    public float MaxPelletScale = 1.0f;

    [DataField]
    public float MinSpeedMultiplier = 0.6f;

    [DataField]
    public float MaxSpeedMultiplier = 1.6f;

    [DataField]
    public int MinBounces = 1;

    [DataField]
    public int MaxBounces = 4;

    // 0-1, networked so the holder's charge meter can read it.
    [AutoNetworkedField]
    public float Charge;

    public TimeSpan? ChargeStart;
    public TimeSpan LastHeld;
    public EntityUid? Shooter;

    // World-space aim, resampled every tick the trigger is held. Storing the client's
    // player-relative coordinates instead lets the shot swing wide when the shooter turns.
    public Vector2 AimDirection;
}
