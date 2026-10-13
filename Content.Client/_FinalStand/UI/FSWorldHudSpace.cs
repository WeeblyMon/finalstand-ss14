using Content.Shared._FinalStand.Repair;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._FinalStand.UI;

public static class FSWorldHudSpace
{
    public static bool BarNorth(IEntityManager entMan, SharedMapSystem map, TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid || !entMan.TryGetComponent<MapGridComponent>(gridUid, out var grid))
            return false;

        var tile = map.TileIndicesFor(gridUid, grid, xform.Coordinates) + new Vector2i(0, 1);
        var anchored = map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (entMan.HasComponent<FSStructureHealthComponent>(ent))
                return true;
        }

        return false;
    }
}
