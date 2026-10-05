// A 3x3 gantry where Engineering and Science take turns assembling a Durand, and where a parked one recharges.
using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Mech;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FSMechBayComponent : Component
{
    [DataField(required: true)]
    public List<FSMechBayStage> Stages = new();

    [DataField]
    public EntProtoId Result = "FSMechDurandBattery";

    [DataField]
    public List<FSMechBayCraft> Crafts = new();

    [AutoNetworkedField]
    public int Stage;

    [DataField]
    public float ParkRange = 1.2f;

    [DataField]
    public float RechargePerSecond = 15f;

    [DataField]
    public float RepairPerSecond = 3f;
}

public enum FSMechBayRole : byte
{
    Engineering,
    Science,
}

[DataDefinition]
public sealed partial class FSMechBayStage
{
    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public FSMechBayRole Role;

    [DataField]
    public Dictionary<ProtoId<StackPrototype>, int> Materials = new();

    [DataField]
    public EntProtoId? Part;

    [DataField]
    public float Time = 5f;

    [DataField(required: true)]
    public string State = string.Empty;
}

[DataDefinition]
public sealed partial class FSMechBayCraft
{
    [DataField(required: true)]
    public EntProtoId Proto;

    [DataField(required: true)]
    public Dictionary<ProtoId<StackPrototype>, int> Materials = new();

    [DataField]
    public float Time = 6f;
}

[Serializable, NetSerializable]
public enum FSMechBayVisualLayers : byte
{
    Stage,
}
