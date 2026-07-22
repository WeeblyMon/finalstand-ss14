using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Perks;

[Serializable, NetSerializable]
public sealed class FSPerksStateEvent : EntityEventArgs
{
    public int PerkPoints;
    public Dictionary<string, int> Levels = new();
    public string[] Slots = new string[FSPerkDef.SlotCount];
    public string[][] Loadouts = [[], [], []];
}

[Serializable, NetSerializable]
public sealed class FSPerkStateRequestMessage : EntityEventArgs { }

[Serializable, NetSerializable]
public sealed class FSPerkStacksUpdateEvent : EntityEventArgs
{
    public string PerkId = "";
    public int Stacks;
}

[Serializable, NetSerializable]
public sealed class FSBuyPerkMessage : EntityEventArgs
{
    public string PerkId = "";
}

[Serializable, NetSerializable]
public sealed class FSEquipPerkMessage : EntityEventArgs
{
    public string PerkId = "";
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
public sealed class FSOpenPerkShopEvent : EntityEventArgs { }

[Serializable, NetSerializable]
public sealed class FSInterestPayoutEvent : EntityEventArgs
{
    public string PerkId = "";
    public int Amount;
}
