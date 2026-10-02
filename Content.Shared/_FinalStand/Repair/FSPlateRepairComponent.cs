using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Repair;

// Repaired by fitting plates of Material, then welding them in. Each plate restores 1/Plates of max health.
[RegisterComponent]
public sealed partial class FSPlateRepairComponent : Component
{
    [DataField(required: true)]
    public ProtoId<StackPrototype> Material;

    [DataField]
    public int Plates = 4;

    public int Fitted;
}
