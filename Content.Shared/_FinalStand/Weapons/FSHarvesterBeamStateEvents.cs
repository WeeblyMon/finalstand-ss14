using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Weapons;

// Broadcast per Harvester shot so FSHarvesterBeamVisualSystem (client) can draw the beam itself.
[Serializable, NetSerializable]
public sealed class FSHarvesterBeamFiredEvent(NetCoordinates fromCoordinates, float angle, float distance, NetEntity? shooter)
    : EntityEventArgs
{
    public readonly NetCoordinates FromCoordinates = fromCoordinates;
    public readonly float Angle = angle;
    public readonly float Distance = distance;
    public readonly NetEntity? Shooter = shooter;
}
