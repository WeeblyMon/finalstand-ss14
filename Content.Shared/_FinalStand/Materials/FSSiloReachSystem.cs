using Content.Shared.Materials.OreSilo;
using Robust.Shared.Containers;

namespace Content.Shared._FinalStand.Materials;

// Reach means any networked silo on the grid within that silo's range; power is ignored.
public sealed partial class FSSiloReachSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSSiloEjectedComponent, EntGotInsertedIntoContainerMessage>((uid, _, _) => RemCompDeferred<FSSiloEjectedComponent>(uid));
    }

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
