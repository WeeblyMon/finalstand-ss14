using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

// Ported from Goob-Station's medigun, minus Uber mode, plus a soft cap that makes the last stretch
// of a patient's health impractical to close with the beam alone.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMediGunComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan? NextTick;

    [ViewVariables, AutoNetworkedField]
    public List<EntityUid> HealedEntities = new();

    // The player holding the gun. Cleared when it stops being held, which drops every link.
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

    // Healing runs at full rate up to this share of the way to crit, then falls away sharply.
    [DataField]
    public float SoftCapRatio = 0.7f;

    // Higher makes the last stretch harder. The curve is asymptotic, so full health is never
    // quite reached by beam alone - that last bit is a doctor's job.
    [DataField]
    public float SoftCapFalloff = 2.5f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundOnTarget;

    [DataField, AutoNetworkedField]
    public Color BeamColor = Color.Aqua;
}

// On the patient, so the client can draw the beam from either end.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMediGunHealedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Source;

    [DataField, AutoNetworkedField]
    public Color BeamColor;
}
