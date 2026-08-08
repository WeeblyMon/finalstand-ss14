// Grid-relative conversion for hitscan effects, matching vanilla HitscanBasicRaycastSystem.
using Robust.Shared.Map;

namespace Content.Shared._FinalStand.Weapons;

public sealed class FSHitscanCoordSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public (EntityCoordinates Coords, Angle Angle) ToGridRelative(EntityCoordinates from, Angle shotAngle)
    {
        var fromXform = Transform(from.EntityId);
        var gridUid = fromXform.GridUid;

        if (gridUid != from.EntityId && TryComp(gridUid, out TransformComponent? gridXform))
        {
            var (_, gridRot, gridInvMatrix) = _transform.GetWorldPositionRotationInvMatrix(gridXform);
            var map = _transform.ToMapCoordinates(from);
            return (new EntityCoordinates(gridUid.Value, System.Numerics.Vector2.Transform(map.Position, gridInvMatrix)),
                    shotAngle - gridRot);
        }

        return (from, shotAngle - _transform.GetWorldRotation(fromXform));
    }
}
