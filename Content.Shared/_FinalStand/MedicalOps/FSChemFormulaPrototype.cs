using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.MedicalOps;

public enum FSChemFormulaKind : byte
{
    Support,
    Hostile,
}

// One thing the chem launcher can fire: the reagent its cloud carries and what a shot costs from the reserve.
[Prototype("fsChemFormula")]
public sealed partial class FSChemFormulaPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name { get; private set; }

    [DataField(required: true)]
    public LocId Purpose { get; private set; }

    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent { get; private set; }

    [DataField(required: true)]
    public EntProtoId Canister { get; private set; }

    [DataField]
    public FSChemFormulaKind Kind { get; private set; } = FSChemFormulaKind.Support;

    [DataField]
    public int Cost { get; private set; } = 20;

    // Debuffs earn the assist cut on their own; damage-only formulas have to deal a share of the kill.
    [DataField]
    public bool Debuff { get; private set; }

    [DataField]
    public ProtoId<FSTechNodePrototype>? Research { get; private set; }

    [DataField]
    public int Priority { get; private set; }
}
