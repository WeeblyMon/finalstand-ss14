// Shared target selection for special-zombie abilities.
using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Station;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Ghost.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._FinalStand.Mobs;

public sealed partial class FSTargetAcquisitionSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly ObjectPool<HashSet<Entity<ActorComponent>>> _actorSetPool =
        new DefaultObjectPool<HashSet<Entity<ActorComponent>>>(
            new SetPolicy<Entity<ActorComponent>>());

    private readonly ObjectPool<HashSet<Entity<FinalStandCCCComponent>>> _cccSetPool =
        new DefaultObjectPool<HashSet<Entity<FinalStandCCCComponent>>>(
            new SetPolicy<Entity<FinalStandCCCComponent>>());

    // Nearest living player-controlled mob. Skips wave zombies, ghosts and the dead.
    public EntityUid? FindNearestPlayer(EntityUid seeker, float range, bool requireLineOfSight = true)
    {
        var origin = _transform.GetMapCoordinates(seeker);

        var candidates = _actorSetPool.Get();
        _lookup.GetEntitiesInRange<ActorComponent>(origin, range, candidates);

        EntityUid? nearest = null;
        var nearestDistSq = float.MaxValue;

        foreach (var (targetUid, _) in candidates)
        {
            if (HasComp<WaveSpawnedTagComponent>(targetUid))
                continue;
            if (HasComp<GhostComponent>(targetUid))
                continue;
            if (TryComp<MobStateComponent>(targetUid, out var mobState) && mobState.CurrentState != MobState.Alive)
                continue;

            var distSq = Vector2.DistanceSquared(origin.Position, _transform.GetMapCoordinates(targetUid).Position);
            if (distSq >= nearestDistSq)
                continue;

            // After the distance test, so the raycast only runs for a candidate that would win.
            if (requireLineOfSight && !_examine.InRangeUnOccluded(seeker, targetUid, range, null))
                continue;

            nearestDistSq = distSq;
            nearest = targetUid;
        }

        _actorSetPool.Return(candidates);
        return nearest;
    }

    public EntityUid? FindCCC(EntityUid seeker, float range)
    {
        var origin = _transform.GetMapCoordinates(seeker);

        var found = _cccSetPool.Get();
        _lookup.GetEntitiesInRange<FinalStandCCCComponent>(origin, range, found);

        EntityUid? ccc = null;
        foreach (var (cccUid, _) in found)
        {
            ccc = cccUid;
            break;
        }

        _cccSetPool.Return(found);
        return ccc;
    }

    // Nearest visible player, else the CCC.
    public EntityUid? AcquireTarget(EntityUid seeker, float range)
        => FindNearestPlayer(seeker, range) ?? FindCCC(seeker, range);
}
