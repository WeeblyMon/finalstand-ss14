using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Augments;

[Serializable, NetSerializable]
public sealed class FSAugmentsStateEvent : EntityEventArgs
{
    public int AugmentPoints;
    public Dictionary<string, int> Levels = new();
    public string[] Slots = new string[FSAugmentDef.SlotCount];
    public string[][] Loadouts = [[], [], []];
}

[Serializable, NetSerializable]
public sealed class FSAugmentStateRequestMessage : EntityEventArgs { }

[Serializable, NetSerializable]
public sealed class FSAugmentStacksUpdateEvent : EntityEventArgs
{
    public string AugId = "";
    public int Stacks;
}

[Serializable, NetSerializable]
public sealed class FSBuyAugmentMessage : EntityEventArgs
{
    public string AugmentId = "";
}

[Serializable, NetSerializable]
public sealed class FSEquipAugmentMessage : EntityEventArgs
{
    public string AugmentId = "";
    public int SlotIndex;
}

[Serializable, NetSerializable]
public sealed class FSUnequipAugmentMessage : EntityEventArgs
{
    public int SlotIndex;
}

[Serializable, NetSerializable]
public sealed class FSSaveLoadoutMessage : EntityEventArgs
{
    public int LoadoutIndex;
}

[Serializable, NetSerializable]
public sealed class FSLoadLoadoutMessage : EntityEventArgs
{
    public int LoadoutIndex;
}

[Serializable, NetSerializable]
public sealed class FSOpenAugmentShopEvent : EntityEventArgs { }
