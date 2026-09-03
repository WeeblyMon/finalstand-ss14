using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Mobs;

// hurled station debris: keeps flying through soft structures until its pierce budget runs out
[RegisterComponent, NetworkedComponent]
public sealed partial class FSGiantBoulderComponent : Component
{
    [DataField]
    public int StructurePierce = 4;

    [DataField]
    public float StructuralDamage = 400f;

    [DataField]
    public float SpinRate = 9f;

    [DataField]
    public EntProtoId ImpactEffect = "FSEffectBoulderImpact";
}
