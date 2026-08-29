using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Sprint;

// hold-to-sprint with stamina drain; exhaustion causes a slow instead of a crit stun
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSSprintComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsSprinting = false;

    [DataField]
    public float SpeedMultiplier = 1.45f;

    [DataField]
    public float StaminaDrainRate = 8f;

    [DataField]
    public float RegenRate = 20f;

    [DataField]
    public float EmptySlowDuration = 5f;

    [DataField]
    public float EmptySlowMultiplier = 0.5f;

    [AutoNetworkedField]
    public float EmptySlowRemaining = 0f;

    [AutoNetworkedField]
    public bool IsExhausted = false;

    public float DustAccumulator = 0f;

    public TimeSpan RegenBlockedUntil = TimeSpan.Zero;
}
