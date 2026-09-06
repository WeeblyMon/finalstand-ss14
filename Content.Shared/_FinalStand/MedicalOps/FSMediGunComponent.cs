using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMediGunComponent : Component
{
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
}
