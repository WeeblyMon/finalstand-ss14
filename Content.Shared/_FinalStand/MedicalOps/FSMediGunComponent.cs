using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMediGunComponent : Component
{
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
    public float Frequency = 0.5f;

    [DataField]
    public int MaxLinksAmount = 1;

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

    // Bone mend per tick, ungated by the soft cap.
    [DataField]
    public FixedPoint2 BoneRepairPerTick = 0.5f;

    // Fraction of Healing applied to limb wounds per tick, ungated by the soft cap.
    [DataField]
    public float LimbHealScale = 0.5f;

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
    // Several medics can beam the same patient; each gun links and unlinks independently.
    [DataField, AutoNetworkedField]
    public List<EntityUid> Sources = new();

    [DataField]
    public bool SoftCapAnnounced;
}
