using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._FinalStand.Research.Prototypes;

// FS-authored equivalent of TechDisciplinePrototype - a rail tab / color grouping for
// FSTechNodePrototype content, kept separate from vanilla disciplines since branches like
// Ordnance don't use the vanilla tier-percentage unlock model.
[Prototype("fsTechBranch")]
public sealed partial class FSTechBranchPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public Color Color;

    [DataField]
    public SpriteSpecifier? Icon;
}
