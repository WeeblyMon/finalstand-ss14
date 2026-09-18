using Content.Shared.Chemistry.Reaction;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.MedicalOps;

[Prototype("fsChemGuide")]
public sealed partial class FSChemGuidePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Category { get; private set; }

    [DataField]
    public int Priority { get; private set; }

    [DataField(required: true)]
    public List<ProtoId<ReactionPrototype>> Reactions { get; private set; } = new();
}
