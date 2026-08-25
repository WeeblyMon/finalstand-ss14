namespace Content.Server._FinalStand.Mobs;

[RegisterComponent]
public sealed partial class FSRevenantBoltComponent : Component
{
    [DataField] public float Damage = 16f;
    [DataField] public float PollInterval = 0.05f;
    [DataField] public float HitCooldown = 0.3f;

    [DataField] public float HitRadius = 0.35f;

    public EntityUid Shooter;
    public float ResistanceBypass;

    public float PollAccum;
    public readonly Dictionary<EntityUid, TimeSpan> LastHitTimes = new();
}
