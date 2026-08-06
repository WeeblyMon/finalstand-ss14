namespace Content.Server._FinalStand.Upgrades.Effects;

// attached to a fired bolt when the wielder has HomingBolts levels; steered in FSHomingProjectileSystem
[RegisterComponent]
public sealed partial class FSHomingProjectileComponent : Component
{
    public float TurnRateDegrees;
    public EntityUid? Target;

    /// <summary>Time of the last target search. Throttles the radius query.</summary>
    public TimeSpan NextSearch;
}
