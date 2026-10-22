namespace Content.Server._FinalStand.Upgrades.Effects;

// attached to a fired bolt when the wielder has HomingBolts levels; steered in FSHomingProjectileSystem
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(FSHomingProjectileSystem))]
public sealed partial class FSHomingProjectileComponent : Component
{
    public float TurnRateDegrees;
    public EntityUid? Target;

    /// <summary>Time of the last target search. Throttles the radius query.</summary>
    [AutoPausedField] public TimeSpan NextSearch;
}
