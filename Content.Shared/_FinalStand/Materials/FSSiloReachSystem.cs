// Whether something can reach the silo network (any networked silo on its grid within that silo's range, power ignored), and which silo holds the pool.
using Content.Shared.Materials.OreSilo;

namespace Content.Shared._FinalStand.Materials;

public sealed partial class FSSiloReachSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public bool InReach(EntityUid client)
    {
        var grid = _transform.GetGrid(client);
        if (grid == null)
            return false;

        var query = EntityQueryEnumerator<FSSiloNetworkComponent, OreSiloComponent, TransformComponent>();
        while (query.MoveNext(out var silo, out _, out var oreSilo, out var xform))
        {
            if (xform.GridUid == grid && _transform.InRange((silo, xform), client, oreSilo.Range))
                return true;
        }

        return false;
    }

    public EntityUid PoolOr(EntityUid silo)
    {
        var query = EntityQueryEnumerator<FSSiloNetworkComponent>();
        while (query.MoveNext(out var uid, out var network))
        {
            if (network.Pool)
                return uid;
        }

        return silo;
    }
}
