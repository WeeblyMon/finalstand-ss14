using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._FinalStand.Research.Prototypes;

// FS-authored equivalent of TechnologyPrototype: server-wide RP-progress-bar research and cross-branch prerequisite joins with vanilla nodes.
[Prototype("fsTechNode")]
public sealed partial class FSTechNodePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = string.Empty;

    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;

    [DataField(required: true)]
    public ProtoId<FSTechBranchPrototype> Branch;

    [DataField(required: true)]
    public int Tier;

    [DataField]
    public int Cost = 5000;

    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int> MaterialCost = new();

    [DataField]
    public List<string> Prerequisites = new();

    [DataField]
    public List<List<string>> PrerequisiteGroups = new();

    [DataField]
    public string? ExclusiveGroup;

    [DataField]
    public LocId BonusDescription = string.Empty;

    [DataField]
    public EntProtoId? WeaponShopUnlock;

    // When set, completing this node grants the referenced vanilla technology's recipe/generic unlocks via ResearchSystem.AddTechnology.
    [DataField]
    public ProtoId<TechnologyPrototype>? VanillaTechnologyId;

    [DataField]
    public bool Hidden;
}
