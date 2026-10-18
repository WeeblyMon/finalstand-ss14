using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Objectives;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSObjectiveMarkerComponent : Component
{
    [DataField, AutoNetworkedField]
    public LocId Label = "fs-objective-marker-target";

    [DataField, AutoNetworkedField]
    public Color? Tint;
}
