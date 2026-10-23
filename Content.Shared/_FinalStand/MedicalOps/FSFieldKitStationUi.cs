using Content.Shared.Chemistry;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

[Serializable, NetSerializable]
public enum FSFieldKitStationUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FSFieldKitStationBuiState : BoundUserInterfaceState
{
    public ContainerInfo? Jug;
    public List<ReagentInventoryItem> Inventory = new();

    public string Label = string.Empty;
    public bool Powered;
    public bool Prep;
    public int Wave;
}

[Serializable, NetSerializable]
public sealed class FSFieldKitSetSlotMessage(int slot, ProtoId<FSChemFormulaPrototype> formula) : BoundUserInterfaceMessage
{
    public readonly int Slot = slot;
    public readonly ProtoId<FSChemFormulaPrototype> Formula = formula;
}

[Serializable, NetSerializable]
public sealed class FSFieldKitBrewTonicMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FSFieldKitDispenseMessage(ItemStorageLocation location, int amount) : BoundUserInterfaceMessage
{
    public readonly ItemStorageLocation Location = location;
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class FSFieldKitDiscardMessage(string reagent) : BoundUserInterfaceMessage
{
    public readonly string Reagent = reagent;
}

[Serializable, NetSerializable]
public sealed class FSFieldKitEmptyJugMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FSFieldKitEjectMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FSFieldKitBottleMessage(int count) : BoundUserInterfaceMessage
{
    public readonly int Count = count;
}

[Serializable, NetSerializable]
public sealed class FSFieldKitPillMessage(int dose, int count) : BoundUserInterfaceMessage
{
    public readonly int Dose = dose;
    public readonly int Count = count;
}

[Serializable, NetSerializable]
public sealed class FSFieldKitLabelMessage(string label) : BoundUserInterfaceMessage
{
    public readonly string Label = label;
}
