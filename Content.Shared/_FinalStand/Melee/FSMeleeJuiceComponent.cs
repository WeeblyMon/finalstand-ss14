using Content.Shared._FinalStand.Particles;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

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
    public FSMeleeHeavyStyle HeavyStyle = FSMeleeHeavyStyle.Swing;

    // Seconds at the weapon's base attack rate before anything else can be swung after a heavy attack.
    [DataField]
    public float HeavyRecovery;

    // Replaces the light profile on the hit after ComboHits light hits in a row.
    [DataField]
    public FSMeleeJuiceProfile? Finisher;

    [DataField]
    public int ComboHits = 2;

    // Seconds after the weapon's cooldown before the combo resets.
    [DataField]
    public float ComboWindow = 0.6f;

    // Played to the attacker when their next hit will be the finisher.
    [DataField]
    public SoundSpecifier? ComboReadySound;

    // Each hit in a row adds this much damage, up to RevStacks, and raises the hit sound's pitch.
    [DataField]
    public float RevStep;

    [DataField]
    public int RevStacks = 6;

    // Seconds between hits before the rev drops, long enough to forgive a missed swing.
    [DataField]
    public float RevWindow = 0.75f;

    // A heavy spends the built-up rev for this much extra damage per stack.
    [DataField]
    public float RevDumpStep;

    // Seconds after the last grind that the rev can still be spent.
    [DataField]
    public float RevDumpWindow = 1.5f;

    [DataField]
    public SoundSpecifier? RevDumpSound;

    // Light swings while sprinting become a running lunge.
    [DataField]
    public bool SprintAttack = true;

    // Hits that always crit with this weapon.
    [DataField]
    public FSMeleeCritWhen CritWhen = FSMeleeCritWhen.Never;

    // Staggered zombies under this fraction of their health can be executed.
    [DataField]
    public float ExecutionHealth = 0.25f;

    // A fully charged heavy sets what it hits and staggers alight.
    [DataField]
    public bool IgniteOnFullCharge;

    // A Sweep heavy dashes through the enemy nearest the cursor within this many tiles.
    [DataField]
    public float SweepRange = 2.5f;

    // Seconds between sweeps; the weapon's other attacks aren't held up by it.
    [DataField]
    public float SweepCooldown = 1.5f;

    // Seconds before the same enemy can be swept through again.
    [DataField]
    public float SweepMarkTime = 5f;

    // Each sweep within this many seconds of the last adds a stack of SweepStackBonus damage.
    [DataField]
    public float SweepStackWindow = 4f;

    [DataField]
    public int SweepMaxStacks = 3;

    [DataField]
    public float SweepStackBonus = 0.25f;

    // A Shot heavy fires this gun from the attacker's inventory, loading it with ShotAmmo.
    [DataField]
    public EntProtoId? ShotWeapon;

    [DataField]
    public EntProtoId? ShotAmmo;

    // The gun as drawn in place of this weapon while it fires.
    [DataField]
    public SpriteSpecifier.Rsi? ShotSprite;

    // Shots held before the first runs out; more come from the gun's magazine upgrades.
    [DataField]
    public int ShotCharges = 1;

    // Seconds to get one shot back.
    [DataField]
    public float ShotRecharge = 2.5f;

    // Seconds between shots while there are charges left.
    [DataField]
    public float ShotInterval = 0.35f;

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

    // Only the closest few mobs in a wide swing are hit; zero hits everything the swing reaches.
    [DataField]
    public int MaxTargets;

    // Tiles the attacker dashes toward the cursor on a heavy attack, hit or miss.
    [DataField]
    public float Dash;

    // A target knocked back by this hit damages mobs it flies into, as a fraction of the hit.
    [DataField]
    public float Launch;

    // Extra mobs behind the target hit in a line.
    [DataField]
    public int Pierce;

    [DataField]
    public float PierceRange = 1.5f;

    // Mobs this close to the target are knocked back and flinch too.
    [DataField]
    public float StaggerRadius;

    // Damage those staggered mobs take, as a fraction of the hit.
    [DataField]
    public float StaggerDamage;

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

    // Tiles in front of the attacker the slash starts at, and how far it then drives forward.
    [DataField]
    public float SlashOffset = 0.5f;

    [DataField]
    public float SlashTravel = 0.4f;

    // Scale the slash grows to by the end of its travel.
    [DataField]
    public float SlashGrow = 1.2f;

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

    // Played once where the swing lands, on top of the weapon's own hit sound.
    [DataField]
    public SoundSpecifier? ImpactSound;

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

    // Seconds ordinary zombies are knocked off their feet for.
    [DataField]
    public float Trip;

    // What the swing that used this copy did, for its effects.
    public int Rev { get; set; }
    public bool Sprint { get; set; }
    public bool Execution { get; set; }
    public bool FullCharge { get; set; }
    public bool Crit { get; set; }

    public FSMeleeJuiceProfile Copy()
    {
        return (FSMeleeJuiceProfile) MemberwiseClone();
    }

    // A copy for a charged swing: damage scales by one factor, and reach, force and feel by the other.
    public FSMeleeJuiceProfile Charged(float damage, float force)
    {
        var profile = Copy();
        profile.DamageMultiplier *= damage;
        profile.Knockback *= force;
        profile.FlinchDuration *= force;
        profile.StaggerRadius *= force;
        profile.Hitstop *= force;
        profile.Trauma = Math.Min(1f, Trauma * force);
        profile.KillZoom *= force;
        profile.SlashScale *= Math.Clamp(force, 0.7f, 1.5f);
        profile.SlashGrow = 1f + (SlashGrow - 1f) * force;
        return profile;
    }
}

public enum FSMeleeHeavyStyle : byte
{
    Swing,
    Spin,
    Slam,
    Shot,
    Sweep,
}

public enum FSMeleeShakeMode : byte
{
    Directional,
    Radial,
    Sustained,
}
