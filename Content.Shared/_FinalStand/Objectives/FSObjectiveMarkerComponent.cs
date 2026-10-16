using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Objectives;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSObjectiveMarkerComponent : Component
{
    [DataField, AutoNetworkedField]
    public LocId Label = "fs-objective-marker-target";
}
