using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Loot;

// Salvage stopgap: wave enemies rarely drop the sheets research consumes. The odds follow the
// number of active scientists, so the drops arrive when someone is there to spend them.
[RegisterComponent]
public sealed partial class FSMaterialDropComponent : Component
{
    [DataField]
    public float BaseChance = 0.002f;

    [DataField]
    public float ChancePerScientist = 0.012f;

    [DataField]
    public float MaxChance = 0.05f;

    [DataField]
    public List<EntProtoId> Materials = new()
    {
        "SheetSteel1",
        "SheetGlass1",
        "SheetPlastic1",
        "SheetPlasma1",
        "IngotGold1",
    };
}
