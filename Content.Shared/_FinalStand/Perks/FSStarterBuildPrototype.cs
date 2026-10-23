using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Perks;

[Prototype("fsStarterBuild")]
public sealed partial class FSStarterBuildPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name { get; private set; }

    [DataField(required: true)]
    public LocId Description { get; private set; }

    [DataField]
    public int Order { get; private set; }

    [DataField(required: true)]
    public List<string> Perks { get; private set; } = new();
}
