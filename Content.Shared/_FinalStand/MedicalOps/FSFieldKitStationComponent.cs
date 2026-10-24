using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.MedicalOps;

// The chemist's bench: launcher loadout and the wave tonic on one side, a dispenser that bottles and presses pills for doctors on the other.
[RegisterComponent, NetworkedComponent]
public sealed partial class FSFieldKitStationComponent : Component
{
    [DataField]
    public string JugSlot = "jug";

    [DataField]
    public EntProtoId BottlePrototype = "ChemistryEmptyBottle01";

    [DataField]
    public EntProtoId PillPrototype = "Pill";

    [DataField]
    public EntProtoId TonicPrototype = "FSTonicCauldron";

    [DataField]
    public FixedPoint2 MaxPillDose = 30;

    [DataField]
    public int MaxPillsPerPress = 20;

    [DataField]
    public string Label = string.Empty;
}
