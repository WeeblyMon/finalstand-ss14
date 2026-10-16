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

    // Replaces the light profile on the hit after ComboHits light hits in a row.
    [DataField]
    public FSMeleeJuiceProfile? Finisher;

    [DataField]
    public int ComboHits = 2;

    // Seconds after the weapon's cooldown before the combo resets.
    [DataField]
    public float ComboWindow = 0.6f;

    public FSMeleeJuiceProfile Get(bool heavy, bool finisher = false)
    {
        if (finisher && Finisher != null)
            return Finisher;

        return heavy && Heavy != null ? Heavy : Light;
    }
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
    public float DamageMultiplier = 1f;

    // Extra mobs behind the target hit in a line.
    [DataField]
    public int Pierce;

    [DataField]
    public float PierceRange = 1.5f;

    // Mobs this close to the target are knocked back and flinch too.
    [DataField]
    public float StaggerRadius;

    [DataField]
    public float Bleed;

    // Extra damage, as a fraction of the hit, against a target that is already flinching.
    [DataField]
    public float FlinchedBonus = 0.25f;

    [DataField]
    public float Knockback;

    // Tiles the attacker steps toward the target on a hit.
    [DataField]
    public float Lunge;

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

    // Degrees added to the hit effect's rotation, for sprites not drawn pointing down.
    [DataField]
    public float HitEffectAngle;

    [DataField]
    public EntProtoId? KillEffect;

    [DataField]
    public EntProtoId? SwingEffect;

    // Smear drawn along the swing, pointing down at rotation zero like the weapon arcs.
    [DataField]
    public EntProtoId? SlashEffect;

    [DataField]
    public Color SlashColor = Color.White;

    [DataField]
    public float SlashScale = 1f;

    [DataField]
    public ProtoId<FSParticleEmitterPrototype>? HitEmitter;

    [DataField]
    public ProtoId<FSParticleEmitterPrototype>? KillEmitter;

    [DataField]
    public Color? ParticleTint;

    [DataField]
    public SoundSpecifier? KillSound;

    // Played where the swing lands on anything that bleeds.
    [DataField]
    public SoundSpecifier? FleshSound;

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
