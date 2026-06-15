using Robust.Shared.Prototypes;

namespace Content.Shared.Body.Prototypes;

[Prototype("body")]
public sealed partial class BodyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name { get; set; } = string.Empty;

    [DataField("root")]
    public string Root { get; set; } = string.Empty;

    [DataField]
    public Dictionary<string, BodyPrototypeSlot> Slots { get; set; } = new();
}

[DataDefinition]
public sealed partial class BodyPrototypeSlot
{
    [DataField("part")]
    public EntProtoId? Part { get; set; }

    [DataField]
    public List<string> Connections { get; set; } = new();

    [DataField]
    public Dictionary<string, EntProtoId> Organs { get; set; } = new();
}
