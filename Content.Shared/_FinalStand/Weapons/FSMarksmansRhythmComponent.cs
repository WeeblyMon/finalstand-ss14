using Robust.Shared.GameObjects;

namespace Content.Shared._FinalStand.Weapons;

[RegisterComponent]
public sealed partial class FSMarksmansRhythmComponent : Component
{
    [DataField] public float DamagePerStack = 0.10f;
    [DataField] public int MaxStacks = 20;
    [DataField] public float DecaySeconds = 10f;
    [DataField] public float MissWindowSeconds = 2.5f;

    // Runtime state — not serialized
    public int CurrentStacks = 0;
    public TimeSpan LastShotTime = TimeSpan.Zero;
    public TimeSpan LastHitTime = TimeSpan.Zero;
    public EntityUid? Shooter;
}
