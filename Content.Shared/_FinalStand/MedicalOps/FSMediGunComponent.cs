using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

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
    [ViewVariables] public FixedPoint2? BaseBoneRepairPerTick;
    [ViewVariables] public float? BaseLimbHealScale;
    [ViewVariables] public float? BaseSplitLinkScale;
    [ViewVariables] public float? BaseMaxCharge;
    [ViewVariables] public float? BaseRechargeRate;
    [ViewVariables] public TimeSpan? BaseRechargePause;
    [ViewVariables] public float? BaseOverhealRatio;
    [ViewVariables] public float? BaseOverhealDecay;

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

    // Free-flying emitters draw the beam from themselves rather than from the medic they serve.
    [DataField]
    public bool BeamFromSelf;

    [DataField, AutoNetworkedField]
    public FSMediGunVariant Variant = FSMediGunVariant.Base;

    // Shield ceiling as a fraction of the patient's crit threshold; 0 means the gun cannot overheal.
    [DataField]
    public float OverhealRatio;

    [DataField]
    public float OverhealPerTick = 4f;

    // Fraction of the shield's ceiling lost per second once it stops being fed.
    [DataField]
    public float OverhealDecay = 0.05f;

    // Percent, 0-100. Built by healing on the UberCharger, drained while an Über runs.
    [DataField, AutoNetworkedField]
    public float UberCharge;

    [DataField]
    public float UberChargePerHp = 0.5f;

    // Trickle while the patient sits at the soft cap, so topping someone off still builds charge.
    [DataField]
    public float UberChargeWhileCapped = 0.4f;

    [DataField]
    public float UberDuration = 10f;

    [DataField]
    public bool UberInfiniteStamina;

    [DataField, AutoNetworkedField]
    public TimeSpan? UberEndTime;

    [DataField]
    public EntProtoId UberActionId = "FSActionMediGunUber";

    [DataField, AutoNetworkedField]
    public EntityUid? UberAction;

    [ViewVariables] public float? BaseUberDuration;

    public bool UberActive => UberEndTime != null;
}

public sealed partial class FSMediGunUberActionEvent : InstantActionEvent;

public enum FSMediGunVariant : byte
{
    Base,
    OverHealer,
    UberCharger,
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
