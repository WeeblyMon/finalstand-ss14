using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.RCD;

// The client asks what would happen at the tile under its RCD ghost; the server answers with its own build rules.
[Serializable, NetSerializable]
public sealed class FSRCDProbeRequestEvent(NetEntity rcd, NetCoordinates location, NetEntity? target) : EntityEventArgs
{
    public readonly NetEntity Rcd = rcd;
    public readonly NetCoordinates Location = location;
    public readonly NetEntity? Target = target;
}

[Serializable, NetSerializable]
public sealed class FSRCDProbeResultEvent(NetEntity rcd, NetCoordinates location, string? text, bool blocked) : EntityEventArgs
{
    public readonly NetEntity Rcd = rcd;
    public readonly NetCoordinates Location = location;
    public readonly string? Text = text;
    public readonly bool Blocked = blocked;
}
