namespace Content.Server._FinalStand.Leveling;

[RegisterComponent]
public sealed partial class FSEnemyDamageTrackerComponent : Component
{
    public readonly Dictionary<EntityUid, float> DamageByPlayer = new();
}
