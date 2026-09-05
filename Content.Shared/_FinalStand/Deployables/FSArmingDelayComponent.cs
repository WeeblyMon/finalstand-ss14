using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Deployables;

// deployed inert, arms itself after a delay so it cannot be dropped onto a target
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSArmingDelayComponent : Component
{
    [DataField]
    public float Delay = 3f;

    [DataField, AutoNetworkedField]
    public bool Armed;

    public TimeSpan ArmAt;
}
