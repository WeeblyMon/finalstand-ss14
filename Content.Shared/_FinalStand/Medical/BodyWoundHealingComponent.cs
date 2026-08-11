// Passive wound healing for a body. Simple bodies (borgs, animals) simply do not carry this.

using Content.Shared._Shitmed.Body;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Medical;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BodyWoundHealingComponent : Component
{
    [DataField]
    public BodyType BodyType = BodyType.Complex;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan HealAt;
}
