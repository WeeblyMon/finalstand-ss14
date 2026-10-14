using Content.Shared._FinalStand.Particles;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Melee;

[RegisterComponent]
public sealed partial class FSMeleeJuiceComponent : Component
{
    [DataField(required: true)]
    public FSMeleeJuiceProfile Light = new();

    [DataField]
    public FSMeleeJuiceProfile? Heavy;

    [DataField]
    public bool HeavyRequiresWield;

    [DataField]
    public bool HeavyIsSpin;

    public FSMeleeJuiceProfile Get(bool heavy) => heavy && Heavy != null ? Heavy : Light;
}

[DataDefinition]
public sealed partial class FSMeleeJuiceProfile
{
    [DataField]
    public float Anticipation = 0.05f;

    [DataField]
    public float Hitstop = 0.05f;

    [DataField]
    public float KillHitstopBonus = 0.02f;

    [DataField]
    public float Reach = 1f;

    [DataField]
    public float Trauma = 0.2f;

    [DataField]
    public FSMeleeShakeMode ShakeMode = FSMeleeShakeMode.Directional;

    [DataField]
    public float Knockback;

    [DataField]
    public float KnockbackDuration = 0.15f;

    [DataField]
    public float FlinchDuration = 0.15f;

    // Kept above zero: a stopped NPC drops its plan.
    [DataField]
    public float FlinchSpeed = 0.4f;

    [DataField]
    public EntProtoId? HitStatus;

    [DataField]
    public float HitStatusDuration = 1f;

    [DataField]
    public float HitStatusSpeed = 0.5f;

    [DataField]
    public EntProtoId? HitEffect;

    [DataField]
    public EntProtoId? KillEffect;

    [DataField]
    public EntProtoId? SwingEffect;

    [DataField]
    public ProtoId<FSParticleEmitterPrototype>? HitEmitter;

    [DataField]
    public ProtoId<FSParticleEmitterPrototype>? KillEmitter;

    [DataField]
    public Color? ParticleTint;

    [DataField]
    public SoundSpecifier? KillSound;

    [DataField]
    public int ExtraStrikes;

    [DataField]
    public float StrikeInterval = 0.12f;

    [DataField]
    public float StrikeDamageFraction = 0.4f;

    [DataField]
    public SoundSpecifier? StrikeSound;

    [DataField]
    public float StrikePitchStep = 0.12f;

    [DataField]
    public float MoveAnticipation = 1f;

    [DataField]
    public float MoveImpact = 1f;

    [DataField]
    public float MoveRecovery = 1f;

    [DataField]
    public float RecoveryTime = 0.3f;

    [DataField]
    public Color TrailColor = Color.White;

    [DataField]
    public float TrailWidth = 0.08f;

    [DataField]
    public float TrailDuration;

    [DataField]
    public float KillZoom = 0.05f;
}

public enum FSMeleeShakeMode : byte
{
    Directional,
    Radial,
    Sustained,
}
