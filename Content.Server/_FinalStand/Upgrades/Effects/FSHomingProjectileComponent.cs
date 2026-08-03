namespace Content.Server._FinalStand.Upgrades.Effects;

// attached to a fired bolt when the wielder has HomingBolts levels; steered in FSHomingProjectileSystem
[RegisterComponent]
public sealed partial class FSHomingProjectileComponent : Component
{
    public float TurnRateDegrees;
    public EntityUid? Target;
}
