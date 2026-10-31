using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Emplacement;

// The unfolded layout is given as if the emplacement faces south.
[RegisterComponent, NetworkedComponent]
public sealed partial class FSEmplacementKitComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Core;

    [DataField]
    public List<FSEmplacementPiece> Pieces = new();

    [DataField]
    public FSEmplacementPiece? ResearchHardpoint;
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
