using Content.Shared.Humanoid.Prototypes;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    [DataField]
    public float Height { get; private set; }

    [DataField]
    public float Width { get; private set; }

    public HumanoidCharacterProfile WithSize(float height, float width)
    {
        return new(this) { Height = height, Width = width };
    }

    private void EnsureValidSize(SpeciesPrototype species)
    {
        Height = species.BodySize.ClampHeight(Height);
        Width = species.BodySize.ClampWidth(Width);
    }
}
