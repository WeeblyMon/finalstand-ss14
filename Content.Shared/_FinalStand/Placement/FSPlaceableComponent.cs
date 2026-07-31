using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Placement;

// Generic "hold item, Z toggles a ghost-preview placement mode, click a spot to confirm" component - reusable by any FS placeable item.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSPlaceableComponent : Component
{
    [DataField(required: true)]
    public EntProtoId PreviewProtoId = default!;

    [DataField]
    public float Range = 10f;

    [DataField, AutoNetworkedField]
    public bool Placing;
}
