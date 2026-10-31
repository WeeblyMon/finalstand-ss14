using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Perks;

// A saved build: the perk levels that were owned, and which of them sat in which slot.
[Serializable, NetSerializable]
public sealed class FSPerkLoadout
{
    // Properties, not fields: the DB copy goes through System.Text.Json, which skips fields entirely.
    public Dictionary<string, int> Levels { get; set; } = new();
    public string[] Slots { get; set; } = new string[FSPerkDef.SlotCount];

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
    public Dictionary<string, int> Mastery = new();
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
public struct FSPerkEarningsEntry
{
    public string PerkId;
    public int WaveCredits;
    public int GameCredits;
    public int WaveResearch;
    public int GameResearch;
}

[Serializable, NetSerializable]
public sealed class FSPerkEarningsSummaryEvent : EntityEventArgs
{
    public List<FSPerkEarningsEntry> Entries = new();
}

[Serializable, NetSerializable]
public sealed class FSApplyStarterBuildMessage : EntityEventArgs
{
    public ProtoId<FSStarterBuildPrototype> BuildId;
}
