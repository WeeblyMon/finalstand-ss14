// A 3x3 gantry where a Broadsword is assembled part by part, and where a parked one recharges.
using System.Linq;
using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Mech;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FSMechBayComponent : Component
{
    [DataField(required: true)]
    public List<FSMechBaySlot> Slots = new();

    [DataField]
    public EntProtoId Result = "FSMechBroadswordBattery";

    [DataField]
    public List<FSMechBayCraft> Crafts = new();

    [DataField]
    public string Sprite = "_FinalStand/Structures/MechBay/broadsword_construction.rsi";

    [DataField]
    public int FrameStates = 18;

    [DataField]
    public string FramePrefix = "broadsword";

    [AutoNetworkedField]
    public List<string> Installed = new();

    [AutoNetworkedField]
    public bool Plated;

    [DataField]
    public float PlatedIntegrity = 1.25f;

    [DataField]
    public float ParkRange = 1.2f;

    [DataField]
    public float RechargePerSecond = 15f;

    [DataField]
    public float RepairPerSecond = 3f;

    public bool Ready => Installed.Count >= Slots.Count;

    public bool CanInstall(FSMechBaySlot slot)
    {
        if (Installed.Contains(slot.Id))
            return false;

        if (slot.Last)
            return Slots.All(s => s.Last || Installed.Contains(s.Id));

        return slot.First || Slots.All(s => !s.First || Installed.Contains(s.Id));
    }

    public FSMechBaySlot? Next()
    {
        return Slots.FirstOrDefault(CanInstall) ?? Slots.FirstOrDefault(s => !Installed.Contains(s.Id));
    }
}

public enum FSMechBayRole : byte
{
    Engineering,
    Science,
}

[DataDefinition]
public sealed partial class FSMechBaySlot
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public FSMechBayRole Role;

    [DataField]
    public Dictionary<ProtoId<StackPrototype>, int> Materials = new();

    [DataField]
    public List<EntProtoId> Parts = new();

    [DataField]
    public EntProtoId? Plate;

    [DataField]
    public float Time = 4f;

    [DataField]
    public bool First;

    [DataField]
    public bool Last;

    [DataField(required: true)]
    public string Icon = string.Empty;

    public bool Weldable => Materials.Count > 0;
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
