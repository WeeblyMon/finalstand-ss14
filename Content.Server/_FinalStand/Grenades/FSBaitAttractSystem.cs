using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._FinalStand.Grenades;
using Content.Shared._FinalStand.NPC;
using Robust.Shared.Containers;
using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._FinalStand.Grenades;

public sealed class FSBaitAttractSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    [Dependency] private readonly IGameTiming _timing = default!;

    private const float AttractRadius = 20f;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(0.25);

    private TimeSpan _nextTick;

    private readonly ObjectPool<HashSet<Entity<HTNComponent>>> _htnSetPool =
        new DefaultObjectPool<HashSet<Entity<HTNComponent>>>(
            new SetPolicy<Entity<HTNComponent>>());

    private readonly List<EntityUid> _released = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSBaitAttractTrackerComponent, ComponentShutdown>(OnTrackerShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextTick)
            return;
        _nextTick = _timing.CurTime + TickInterval;

        var query = EntityQueryEnumerator<FSBaitDecoyComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (_container.IsEntityInContainer(uid))
                continue;

            var tracker = EnsureComp<FSBaitAttractTrackerComponent>(uid);
            var worldPos = _transform.GetWorldPosition(uid);
            var coords = new MapCoordinates(worldPos, xform.MapID);

            var zombieCandidates = _htnSetPool.Get();
            _lookup.GetEntitiesInRange<HTNComponent>(coords, AttractRadius, zombieCandidates);

            foreach (var (npcUid, _) in zombieCandidates)
            {
                // Re-asserted every tick. Other branches clear the key, and a one-shot set
                // meant a zombie that lost it never re-acquired the bait.
                tracker.ZombieToBait[npcUid] = uid;
                _npc.SetBlackboard(npcUid, FSAIBlackboardKeys.BaitTarget, uid);
            }

            // A zombie that walked out of range keeps chasing the bait forever otherwise.
            _released.Clear();
            foreach (var (npcUid, _) in tracker.ZombieToBait)
            {
                if (!InRange(npcUid, coords))
                    _released.Add(npcUid);
            }

            foreach (var npcUid in _released)
            {
                tracker.ZombieToBait.Remove(npcUid);
                if (TryComp<HTNComponent>(npcUid, out var htn))
                    htn.Blackboard.Remove<EntityUid>(FSAIBlackboardKeys.BaitTarget);
            }

            _htnSetPool.Return(zombieCandidates);
        }
    }

    private bool InRange(EntityUid npcUid, MapCoordinates baitCoords)
    {
        if (!TryComp<TransformComponent>(npcUid, out var xform) || xform.MapID != baitCoords.MapId)
            return false;

        var pos = _transform.GetWorldPosition(xform);
        return (pos - baitCoords.Position).LengthSquared() <= AttractRadius * AttractRadius;
    }

    private void OnTrackerShutdown(Entity<FSBaitAttractTrackerComponent> ent, ref ComponentShutdown args)
    {
        foreach (var (zombieUid, _) in ent.Comp.ZombieToBait)
        {
            if (!TryComp<HTNComponent>(zombieUid, out var htn))
                continue;
            htn.Blackboard.Remove<EntityUid>(FSAIBlackboardKeys.BaitTarget);
        }
    }
}
