using Content.Shared._Shitmed.Body;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Medical;

// Simple bodies (borgs, animals) don't carry this.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BodyWoundHealingComponent : Component
{
    [DataField]
    public BodyType BodyType = BodyType.Complex;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan HealAt;
}
