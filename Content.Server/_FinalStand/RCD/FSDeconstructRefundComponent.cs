using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.RCD;

// Materials returned when an RCD deconstructs this, scaled by its remaining health.
[RegisterComponent]
public sealed partial class FSDeconstructRefundComponent : Component
{
    [DataField(required: true)]
    public Dictionary<EntProtoId, int> Materials = new();

    [DataField]
    public float Fraction = 0.5f;
}
