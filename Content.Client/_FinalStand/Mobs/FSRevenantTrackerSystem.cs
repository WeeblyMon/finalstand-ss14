using System.Numerics;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.Player;
using Robust.Shared.Map;

namespace Content.Client._FinalStand.Mobs;

public sealed class FSRevenantTrackerSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public bool TryGetNearest(out EntityUid nearest, out float distance)
    {
        nearest = default;
        distance = float.MaxValue;

        if (_player.LocalSession?.AttachedEntity is not { } local)
            return false;

        var localPos = _transform.GetMapCoordinates(local);
        if (localPos.MapId == MapId.Nullspace)
            return false;

        var query = EntityQueryEnumerator<FSRevenantComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState, out var xform))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;

            var pos = _transform.GetMapCoordinates(xform);
            if (pos.MapId != localPos.MapId)
                continue;

            var candidate = Vector2.Distance(localPos.Position, pos.Position);
            if (candidate >= distance)
                continue;

            distance = candidate;
            nearest = uid;
        }

        return distance < float.MaxValue;
    }
}
