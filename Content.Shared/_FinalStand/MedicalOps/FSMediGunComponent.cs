using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMediGunComponent : Component
{
    // Prototype values, captured once at MapInit. Research applies deltas over these rather than
    // assigning absolutes, so the YAML stays the authority for the baseline.
    [ViewVariables] public float? BaseMaxRange;
    [ViewVariables] public FixedPoint2? BaseBleedingAmountModifier;
    [ViewVariables] public int? BaseMaxLinksAmount;
    [ViewVariables] public float? BaseFrequency;
    [ViewVariables] public float? BaseBatteryWithdraw;
    [ViewVariables] public float? BaseSoftCapRatio;

    [DataField, AutoNetworkedField]
    public TimeSpan? NextTick;

    [ViewVariables, AutoNetworkedField]
    public List<EntityUid> HealedEntities = new();

    [ViewVariables, AutoNetworkedField]
    public EntityUid? ParentEntity;

    [ViewVariables]
    public bool IsActive;

    [DataField]
    public EntityWhitelist HealAbleWhitelist = new()
    {
        RequireAll = true,
        Components = new[] { "Damageable", "MobState" },
    };

    [DataField(required: true)]
    public DamageSpecifier Healing = new();

    [DataField]
    public FixedPoint2 BleedingAmountModifier = 3;

    [DataField]
    public float Frequency = 1f;

    [DataField]
    public int MaxLinksAmount = 1;

    /// <summary>Per-beam output once more than one patient is attached.</summary>
    [DataField]
    public float SplitLinkScale = 0.6f;

    [DataField]
    public float MaxRange = 6f;

    [DataField]
    public float BatteryWithdraw = 0.5f;

    [DataField]
    public float SoftCapRatio = 0.7f;

    [DataField]
    public float SoftCapFalloff = 4.5f;

    [DataField]
    public float MinEffectiveScale = 0.05f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundOnTarget;

    [DataField]
    public SoundSpecifier? SoundOnTargetLost =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/medigun_target_lost.ogg");

    [DataField]
    public SoundSpecifier? SoundOnSoftCap =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/medigun_cap_reached.ogg");

    [DataField, AutoNetworkedField]
    public Color BeamColor = Color.FromHex("#E23B3B");
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMediGunHealedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Source;

    [DataField, AutoNetworkedField]
    public Color BeamColor;

    // Latches so crossing the soft cap sounds once instead of every heal tick.
    [DataField]
    public bool SoftCapAnnounced;
}
