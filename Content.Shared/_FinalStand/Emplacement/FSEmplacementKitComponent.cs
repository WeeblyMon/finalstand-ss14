// The Chief Engineer's folded emplacement: what it unfolds into, laid out as if it faces south.
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Emplacement;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSEmplacementKitComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Core;

    [DataField]
    public List<FSEmplacementPiece> Pieces = new();
}

[DataDefinition]
public sealed partial class FSEmplacementPiece
{
    [DataField(required: true)]
    public EntProtoId Proto;

    [DataField]
    public Vector2i Offset;

    [DataField]
    public Direction Facing = Direction.South;
}
