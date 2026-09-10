using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.MedicalOps;

[Prototype("fsSurgeryGuide")]
public sealed partial class FSSurgeryGuidePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<EntProtoId> Goals { get; private set; } = new();

    [DataField]
    public List<EntProtoId> Access { get; private set; } = new();

    [DataField]
    public List<EntProtoId> Closing { get; private set; } = new();
}
