using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Titles;

[Prototype("fsTitle")]
public sealed partial class FSTitlePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name { get; private set; }

    [DataField(required: true)]
    public LocId Description { get; private set; }

    [DataField]
    public Color Color { get; private set; } = Color.FromHex("#D4AF37");
}
