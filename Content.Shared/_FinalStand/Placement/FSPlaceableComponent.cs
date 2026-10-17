using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Placement;

// Hold the item, Z toggles a ghost preview, click a spot to confirm. Reusable by any FS placeable item.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSPlaceableComponent : Component
{
    [DataField(required: true)]
    public EntProtoId PreviewProtoId = default!;

    [DataField]
    public float Range = 10f;

    [DataField]
    public bool Rotates;

    [DataField, AutoNetworkedField]
    public bool Placing;

    public Direction PlacementDirection = Direction.South;
}
