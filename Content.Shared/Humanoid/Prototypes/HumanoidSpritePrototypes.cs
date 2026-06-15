using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Humanoid.Prototypes;

[Prototype("speciesBaseSprites")]
public sealed partial class HumanoidSpeciesBaseSpritesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public Dictionary<HumanoidVisualLayers, string> Sprites { get; set; } = new();
}

[Prototype("humanoidBaseSprite")]
public sealed partial class HumanoidSpeciesSpriteLayer : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public bool MatchSkin { get; set; } = false;

    [DataField]
    public bool MatchEye { get; set; } = false;

    /// <summary>
    /// Goob extension: allows specifying both the RSI path and state together.
    /// Vanilla only used this as a plain string (RSI path); Goob changed it to SpriteSpecifier
    /// so the state can also be specified (e.g. cybernetic limb sprites with multiple states).
    /// </summary>
    [DataField]
    public SpriteSpecifier? BaseSprite { get; set; }
}
