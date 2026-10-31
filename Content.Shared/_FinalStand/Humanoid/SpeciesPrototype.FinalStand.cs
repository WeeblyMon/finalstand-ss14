using Content.Shared._FinalStand.Humanoid;

namespace Content.Shared.Humanoid.Prototypes;

public sealed partial class SpeciesPrototype
{
    [DataField]
    public FSBodySize BodySize = new();
}
