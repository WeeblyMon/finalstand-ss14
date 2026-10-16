using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Titles;

[Serializable, NetSerializable]
public sealed class FSTitlesRequestEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class FSTitleEquipEvent : EntityEventArgs
{
    public string? Title;
}

[Serializable, NetSerializable]
public sealed class FSTitlesStateEvent : EntityEventArgs
{
    public List<string> Owned = new();
    public string? Equipped;
}

[Serializable, NetSerializable]
public sealed class FSMentorRequestEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class FSMentorBuyEvent : EntityEventArgs
{
    public string Item = string.Empty;
}

[Serializable, NetSerializable]
public sealed record FSMentorShopEntry(string Id, int Cost, string Title, bool Owned);

[Serializable, NetSerializable]
public sealed class FSMentorStateEvent : EntityEventArgs
{
    public int Balance;
    public int Total;
    public int WeekPoints;
    public int WeekCap;
    public bool Eligible;
    public double Hours;
    public double MinHours;
    public int Level;
    public int MinLevel;
    public List<FSMentorShopEntry> Shop = new();
    public string? Message;
}
