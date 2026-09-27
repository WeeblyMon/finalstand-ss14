using Content.Shared.Chemistry;
using Content.Shared.FixedPoint;
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
    public FixedPoint2 Tank;
    public FixedPoint2 TankMax;

    public ContainerInfo? Jug;
    public List<ReagentInventoryItem> Inventory = new();

    public string? MagazineName;
    public FixedPoint2 MagazineVolume;
    public FixedPoint2 MagazineMax;

    public List<FSFlaskStock> Flasks = new();

    public string Label = string.Empty;
    public bool Powered;
}

// Empty flasks in the rack of one capacity, so heavy and standard flasks can be filled separately.
[Serializable, NetSerializable]
public sealed record FSFlaskStock(FixedPoint2 Capacity, int Count);

[Serializable, NetSerializable]
public sealed class FSFieldKitDumpSatchelMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FSFieldKitBrewMessage(ProtoId<FSFieldKitPrototype> kit, int volume) : BoundUserInterfaceMessage
{
    public readonly ProtoId<FSFieldKitPrototype> Kit = kit;
    public readonly int Volume = volume;
}

// Location is null for the tissue tank.
[Serializable, NetSerializable]
public sealed class FSFieldKitDispenseMessage(ItemStorageLocation? location, int amount) : BoundUserInterfaceMessage
{
    public readonly ItemStorageLocation? Location = location;
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
public sealed class FSFieldKitEjectMessage(bool magazine) : BoundUserInterfaceMessage
{
    public readonly bool Magazine = magazine;
}

[Serializable, NetSerializable]
public sealed class FSFieldKitLoadMagazineMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FSFieldKitFillFlasksMessage(FixedPoint2 capacity, int count) : BoundUserInterfaceMessage
{
    public readonly FixedPoint2 Capacity = capacity;
    public readonly int Count = count;
}

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
