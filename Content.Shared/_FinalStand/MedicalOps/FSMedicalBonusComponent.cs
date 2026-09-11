using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMedicalBonusComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<string, FSMedicalBuff> Active = new();
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class FSMedicalBuff
{
    [DataField]
    public Dictionary<FSMedicalBonusCategory, float> Bonuses = new();

    [DataField]
    public TimeSpan? EndTime;

    [DataField]
    public string? Name;

    public bool IsExpired(TimeSpan now) => EndTime is { } end && end <= now;
}

[Serializable, NetSerializable]
public enum FSMedicalBonusCategory : byte
{
    TreatmentSpeed,
    RevivalSpeed,
    Stabilisation,
    DefibCooldown,
    Movement,
    DragSpeed,
    InterruptionResistance,
    MeleeSpeed,
}
