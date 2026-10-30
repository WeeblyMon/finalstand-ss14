using Content.Shared.Actions;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMediGunComponent : Component
{
    public const float DefaultSoftCap = 0.7f;

    [DataField(required: true)]
    public DamageSpecifier HealPerPulse = new();

    [DataField]
    public float PulseSeconds = 0.5f;

    [DataField]
    public float Range = 6f;

    [DataField]
    public int MaxPatients = 1;

    [DataField]
    public float SplitLinkScale = 0.6f;

    [DataField]
    public float ChargePerPulse = 0.5f;

    [DataField]
    public float ClotPerSecond = 0.5f;

    [DataField]
    public bool CanSetBones;

    [DataField]
    public float BoneSetSeconds = 6f;

    [DataField]
    public float SplintedSetSeconds = 3f;

    [DataField]
    public bool CanClearBurns;

    [DataField]
    public float BurnClearSeconds = 6f;

    [DataField]
    public float SoftCapRatio = DefaultSoftCap;

    [DataField]
    public float SoftCapFalloff = 4.5f;

    [DataField]
    public float MinEffectiveScale = 0.05f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? LinkSound;

    [DataField]
    public SoundSpecifier? LostSound = new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/medigun_target_lost.ogg");

    [DataField]
    public SoundSpecifier? CapSound = new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/medigun_cap_reached.ogg");

    [DataField, AutoNetworkedField]
    public Color BeamColor = Color.FromHex("#E23B3B");

    [DataField]
    public bool BeamFromSelf;

    [DataField, AutoNetworkedField]
    public FSMediGunVariant Variant = FSMediGunVariant.Base;

    [DataField]
    public float OverhealRatio;

    [DataField]
    public float OverhealPerTick = 4f;

    [DataField]
    public float OverhealDecay = 0.05f;

    [DataField, AutoNetworkedField]
    public float UberCharge;

    [DataField]
    public float UberChargePerHp = 0.25f;

    [DataField]
    public float UberChargeWhileCapped = 0.2f;

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

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Medic;

    [ViewVariables, AutoNetworkedField]
    public List<EntityUid> Patients = new();

    [ViewVariables]
    public TimeSpan NextPulse;

    [ViewVariables]
    public EntityUid? UberLoop;

    [ViewVariables] public float? BaseRange;
    [ViewVariables] public float? BaseClotPerSecond;
    [ViewVariables] public int? BaseMaxPatients;
    [ViewVariables] public float? BasePulseSeconds;
    [ViewVariables] public float? BaseChargePerPulse;
    [ViewVariables] public float? BaseSoftCapRatio;
    [ViewVariables] public float? BaseSplitLinkScale;
    [ViewVariables] public float? BaseMaxCharge;
    [ViewVariables] public float? BaseRechargeRate;
    [ViewVariables] public TimeSpan? BaseRechargePause;
    [ViewVariables] public float? BaseOverhealRatio;
    [ViewVariables] public float? BaseOverhealDecay;
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
    [DataField, AutoNetworkedField]
    public List<EntityUid> Sources = new();

    [DataField]
    public bool SoftCapAnnounced;
}
