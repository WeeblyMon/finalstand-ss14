// One-off visual moments the server tells nearby clients about; the client draws them with its particle system.
using Robust.Shared.Map;
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
