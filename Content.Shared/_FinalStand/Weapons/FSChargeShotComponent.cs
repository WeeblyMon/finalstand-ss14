using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._FinalStand.Weapons;

// hold to charge: longer holds hit harder, pierce further and throw bigger pellets
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

    // 0-1, networked so the holder's charge meter can read it.
    [AutoNetworkedField]
    public float Charge;

    public TimeSpan? ChargeStart;
    public TimeSpan LastHeld;
    public EntityCoordinates? ShootCoordinates;
    public EntityUid? Shooter;
}
