// Where an emplacement may unfold: every tile of its footprint must be placeable. Shared so the ghost and the server agree.
using System.Numerics;
using Content.Shared._FinalStand.Placement;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._FinalStand.Emplacement;

public sealed partial class FSEmplacementRuleSystem : EntitySystem
{
    [Dependency] private FSPlacementRuleSystem _rules = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedMapSystem _map = default!;

    public EntityCoordinates Snap(EntityCoordinates coords)
    {
        if (_transform.GetGrid(coords) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return coords;

        return _map.GridTileToLocal(gridUid, grid, _map.TileIndicesFor(gridUid, grid, coords));
    }

    public static EntityCoordinates TileOf(EntityCoordinates center, Vector2i offset, Direction facing)
    {
        return center.Offset(facing.ToAngle().RotateVec(new Vector2(offset.X, offset.Y)));
    }

    public static Angle RotationOf(FSEmplacementPiece piece, Direction facing)
    {
        return piece.Facing.ToAngle() + facing.ToAngle();
    }

    public IEnumerable<EntityCoordinates> Footprint(EntityCoordinates center, Direction facing, FSEmplacementKitComponent kit)
    {
        var seen = new HashSet<Vector2i> { Vector2i.Zero };
        yield return center;
        foreach (var piece in kit.Pieces)
        {
            if (seen.Add(piece.Offset))
                yield return TileOf(center, piece.Offset, facing);
        }
    }

    public bool CanPlace(EntityUid user, EntityCoordinates center, Direction facing, FSEmplacementKitComponent kit, float range)
    {
        if (!_transform.InRange(Transform(user).Coordinates, center, range))
            return false;

        foreach (var tile in Footprint(center, facing, kit))
        {
            if (!_turf.TryGetTileRef(tile, out var tileRef)
                || _turf.IsSpace(tileRef.Value)
                || _turf.IsTileBlocked(tileRef.Value, CollisionGroup.Impassable)
                || !_rules.IsTileBuildable(tile, blocksPath: true))
                return false;
        }

        return true;
    }
}
