using Content.Shared.Explosion;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Deployables;

// tunable mine payload, carried on the item so shop upgrades survive into the planted mine
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSLandmineComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Detonations = 1;

    [DataField]
    public ProtoId<ExplosionPrototype> ExplosionType = "FSLandmineExplosion";

    [DataField]
    public float IntensitySlope = 4f;

    [DataField]
    public float TotalIntensity = 9.6f;

    [DataField]
    public float MaxIntensity = 4f;

    [DataField]
    public float IntensityMultiplier = 1f;

    [DataField]
    public bool HighExplosive;

    [DataField]
    public float HighExplosiveTotalIntensity = 45f;

    [DataField]
    public float HighExplosiveMaxIntensity = 6f;
}
