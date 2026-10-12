using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Deployables;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSDeployableFieldVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Scale = 1f;

    [DataField]
    public string LayerKey = "decal";
}
