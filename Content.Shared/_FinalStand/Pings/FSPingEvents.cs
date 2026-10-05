// Ping types, who they're for, and the messages that carry pings and the per-role task board between client and server.
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Pings;

[Serializable, NetSerializable]
public enum FSPingType : byte
{
    Go,
    Danger,
    Enemy,
    Special,
    Boss,
    Defend,
    FallBack,
    NeedAmmo,
    NeedChems,
    NeedRepair,
    Repair,
    BuildHere,
    Revive,
    Ammo,
    Loot,
    Bay,
    Door,
    Acknowledge,
}

[Serializable, NetSerializable]
public enum FSPingAudience : byte
{
    Everyone,
    Engineering,
    Medical,
    Science,
}

[Serializable, NetSerializable]
public sealed class FSPingRequestEvent(NetCoordinates coords, NetEntity? target, FSPingType? forced) : EntityEventArgs
{
    public readonly NetCoordinates Coords = coords;
    public readonly NetEntity? Target = target;
    public readonly FSPingType? Forced = forced;
}

[Serializable, NetSerializable]
public sealed class FSPingEvent : EntityEventArgs
{
    public int Id;
    public NetEntity Sender;
    public string SenderName = string.Empty;
    public FSPingType Type;
    public FSPingAudience Audience;
    public NetCoordinates Coords;
    public NetEntity? Target;
    public string Label = string.Empty;
    public string? IconPath;
    public string? BuildProto;
    public float Lifetime;
}

[Serializable, NetSerializable]
public sealed class FSTaskEntry
{
    public int Id;
    public FSPingType Type;
    public string Label = string.Empty;
    public NetCoordinates Coords;
    public NetEntity? Target;
    public string? ClaimedBy;
    public string Requester = string.Empty;
}

[Serializable, NetSerializable]
public sealed class FSTaskBoardEvent(List<FSTaskEntry> tasks) : EntityEventArgs
{
    public readonly List<FSTaskEntry> Tasks = tasks;
}
