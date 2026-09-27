using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.MedicalOps;

// The chemist's bench: a reagent dispenser with a shared tissue tank that brews field kits and packages them.
[RegisterComponent, NetworkedComponent]
public sealed partial class FSFieldKitStationComponent : Component
{
    [DataField]
    public string JugSlot = "jug";

    [DataField]
    public string MagazineSlot = "magazine";

    [DataField]
    public string MagazineSolution = "pack";

    [DataField]
    public string TankSolution = "tank";

    [DataField]
    public string RackContainer = "flaskRack";

    [DataField]
    public int RackCapacity = 12;

    [DataField]
    public ProtoId<ReagentPrototype> TissueReagent = "FSBiomass";

    [DataField]
    public EntProtoId BottlePrototype = "ChemistryEmptyBottle01";

    [DataField]
    public EntProtoId PillPrototype = "Pill";

    [DataField]
    public FixedPoint2 MaxPillDose = 30;

    [DataField]
    public int MaxPillsPerPress = 20;

    [DataField]
    public string Label = string.Empty;
}
