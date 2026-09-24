using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Loot;

// Salvage stopgap: wave enemies rarely drop the sheets research consumes.
[RegisterComponent]
public sealed partial class FSMaterialDropComponent : Component
{
    [DataField]
    public float DropChance = 0.2f;

    [DataField]
    public List<EntProtoId> Materials = new()
    {
        "SheetSteel1",
        "SheetGlass1",
        "SheetPlastic1",
        "SheetPlasma1",
        "IngotGold1",
        "IngotSilver1",
        "SheetUranium1",
    };
}
