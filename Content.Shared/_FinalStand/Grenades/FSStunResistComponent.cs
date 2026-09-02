namespace Content.Shared._FinalStand.Grenades;

/// <summary>
/// Reduces the stun duration applied by FSStunInRadiusOnTrigger (flash grenades).
/// </summary>
[RegisterComponent]
public sealed partial class FSStunResistComponent : Component
{
    /// <summary>Fraction of the original stun duration to actually apply. 0.25 = 75% reduction.</summary>
    [DataField] public float DurationMultiplier = 0.25f;
}
