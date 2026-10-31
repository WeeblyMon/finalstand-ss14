using Content.Shared._FinalStand.Departments;
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

public static class FSPingAudiences
{
    public static bool Includes(FSPingAudience audience, FSDepartmentAccessComponent? dept)
    {
        return audience switch
        {
            FSPingAudience.Everyone => true,
            FSPingAudience.Engineering => dept?.Engineering == true,
            FSPingAudience.Medical => dept?.Medical == true,
            FSPingAudience.Science => dept?.Science == true,
            _ => false,
        };
    }
}

[Serializable, NetSerializable]
public sealed class FSPingRequestEvent(NetCoordinates coords, NetEntity? target, FSPingType? forced) : EntityEventArgs
{
    public readonly NetCoordinates Coords = coords;
    public readonly NetEntity? Target = target;
    public readonly FSPingType? Forced = forced;
}

[Serializable, NetSerializable]
public sealed class FSPingClearedEvent(int id) : EntityEventArgs
{
    public readonly int Id = id;
}

[Serializable, NetSerializable]
public sealed class FSPingRefusedEvent(float seconds) : EntityEventArgs
{
    public readonly float Seconds = seconds;
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
