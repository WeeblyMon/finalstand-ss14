using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Ailments;

[Prototype("fsAilmentRules")]
public sealed partial class FSAilmentRulesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public float InjuryFloor = 5f;

    [DataField]
    public int MajorBleedLevel = 2;

    [DataField]
    public EntProtoId Broken = "FSAilmentBroken";

    [DataField]
    public EntProtoId Burned = "FSAilmentBurned";

    [DataField]
    public float WarningLow = 40f;

    [DataField]
    public float WarningHigh = 70f;

    [DataField]
    public Dictionary<FSAilmentMeter, FSAilmentMeterRule> Meters = new();

    [DataField(required: true)]
    public DamageSpecifier BleedDamage = new();

    [DataField(required: true)]
    public DamageSpecifier DownDrain = new();

    [DataField]
    public TimeSpan ClockWindow = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan ClockResendDrift = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan RegenDelay = TimeSpan.FromSeconds(6);

    [DataField]
    public float RegenPerSecond = 0.5f;

    [DataField]
    public float RegenCap = 0.5f;
}

[DataDefinition]
public sealed partial class FSAilmentMeterRule
{
    [DataField(required: true)]
    public FSAilmentSlot Slot;

    [DataField]
    public float DrainPerSecond = 10f;

    [DataField(required: true)]
    public FSInjurySpec Injury = new();
}
