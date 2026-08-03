using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.RiotShield;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSRiotShieldComponent : Component
{
    [DataField, AutoNetworkedField] public bool IsBroken;

    [DataField] public float BaseDurability = 200f;

    [DataField, AutoNetworkedField] public float DurabilityMultiplier = 1f;

    /// <summary>Current HP remaining; decremented by blocking damage, restored on wave prep.</summary>
    [DataField, AutoNetworkedField] public float CurrentDurability = 200f;

    [DataField] public float ThornsPercent;

    [DataField] public float VampirePercent;

    public EntityUid? Wielder;
}
