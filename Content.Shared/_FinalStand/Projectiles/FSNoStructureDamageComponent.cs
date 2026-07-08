namespace Content.Shared._FinalStand.Projectiles;

/// <summary>
///     Projectiles with this component will not deal damage to structures or objects —
///     only entities with MobStateComponent (living mobs) take damage.
/// </summary>
[RegisterComponent]
public sealed partial class FSNoStructureDamageComponent : Component { }
