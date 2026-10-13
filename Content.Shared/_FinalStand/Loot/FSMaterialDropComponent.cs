using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Loot;

// Materials a wave enemy drops on death: a per-tier pool (repeats weight it), with a chance that grows each wave.
[RegisterComponent]
public sealed partial class FSMaterialDropComponent : Component
{
    [DataField]
    public float DropChance = 0.25f;

    [DataField]
    public float ChancePerWave = 0.005f;

    [DataField]
    public float MaxChance = 0.45f;

    [DataField]
    public int Rolls = 1;

    [DataField]
    public List<EntProtoId> Materials = new()
    {
        "SheetSteel1",
        "SheetSteel1",
        "SheetSteel1",
        "SheetGlass1",
        "MaterialCloth1",
    };
}
