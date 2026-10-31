using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Effects;

[Serializable, NetSerializable]
public sealed class FSFlairBurstEvent(string emitter, NetCoordinates coords, Color? tint, float delay, float count, float kick)
    : EntityEventArgs
{
    public readonly string Emitter = emitter;
    public readonly NetCoordinates Coords = coords;
    public readonly Color? Tint = tint;
    public readonly float Delay = delay;
    public readonly float Count = count;
    public readonly float Kick = kick;
}

[Serializable, NetSerializable]
public sealed class FSBuildHologramEvent(NetCoordinates coords, string piece, float duration, Angle rotation) : EntityEventArgs
{
    public readonly NetCoordinates Coords = coords;
    public readonly string Piece = piece;
    public readonly float Duration = duration;
    public readonly Angle Rotation = rotation;
}

// Lightning-style beams are pure decoration: the client lays the segments itself instead of the server
// spawning a networked entity for every tile of every arc.
[Serializable, NetSerializable]
public sealed class FSBeamEvent(MapCoordinates from, MapCoordinates to, EntProtoId segment) : EntityEventArgs
{
    public readonly MapCoordinates From = from;
    public readonly MapCoordinates To = to;
    public readonly EntProtoId Segment = segment;
}
