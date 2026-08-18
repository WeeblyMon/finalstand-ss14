using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Perks;

// A saved build: the perk levels that were owned, and which of them sat in which slot.
[Serializable, NetSerializable]
public sealed class FSPerkLoadout
{
    public Dictionary<string, int> Levels = new();
    public string[] Slots = new string[FSPerkDef.SlotCount];

    public FSPerkLoadout()
    {
        for (var i = 0; i < FSPerkDef.SlotCount; i++)
            Slots[i] = string.Empty;
    }

    public bool IsEmpty => Levels.Count == 0;
}

[Serializable, NetSerializable]
public sealed class FSPerksStateEvent : EntityEventArgs
{
    public int PerkPoints;
    public Dictionary<string, int> Levels = new();
    public string[] Slots = new string[FSPerkDef.SlotCount];
    public FSPerkLoadout[] Loadouts = [new(), new(), new()];
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
public sealed class FSUnequipPerkMessage : EntityEventArgs
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
public sealed class FSRespecPerkMessage : EntityEventArgs { }

[Serializable, NetSerializable]
public sealed class FSInterestPayoutEvent : EntityEventArgs
{
    public string PerkId = "";
    public int Amount;
}
