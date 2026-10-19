namespace Content.Server._FinalStand.Leveling;

[RegisterComponent]
[Access(typeof(FSEnemyDamageTrackerSystem))]
public sealed partial class FSEnemyDamageTrackerComponent : Component
{
    public readonly Dictionary<EntityUid, float> DamageByPlayer = new();
}
