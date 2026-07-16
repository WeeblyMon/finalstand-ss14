namespace Content.Shared._FinalStand.Upgrades.Effects;

[RegisterComponent]
public sealed partial class FSBarrageComponent : Component
{
    [DataField] public int Level = 1;
    [DataField] public float Spool = 0f;
    [DataField] public double LastShotTime = 0;

    public const float SpoolGainPerShot = 0.15f;
    public const float SpoolDecayDelay = 0.8f;
    public const float SpoolDecayRate = 0.3f;
    public const float FireRateBonusPerLevel = 0.6f;
    public const float ExplosionBonusPerLevel = 0.3f;
}
