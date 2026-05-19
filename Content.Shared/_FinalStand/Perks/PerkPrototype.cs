using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._FinalStand.Perks;

[Prototype("fsPerk")]
public sealed partial class PerkPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name = "";

    [DataField]
    public string Description = "";

    [DataField]
    public int Cost = 15000;

    [DataField("perkType")]
    public PerkType PerkType = default!;

    [DataField]
    public bool IsLocked = false;

    // TODO(finalstand): replace with real perk icons — spritework ticket
    [DataField]
    public SpriteSpecifier.Texture? Icon = null;
}
