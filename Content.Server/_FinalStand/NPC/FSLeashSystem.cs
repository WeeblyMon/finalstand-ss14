using Content.Server._FinalStand.Spawners;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.NPC;
using Content.Shared.Examine;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.NPC;

// Limits how far wave enemies will chase a target before giving up and returning to CCC pathing.
public sealed partial class FSLeashSystem : EntitySystem
{
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // TODO(finalstand): tune leash distance
    private const float LeashDistance = 10f;
    // TODO(finalstand): tune LOS timeout
    private static readonly TimeSpan LosTimeout = TimeSpan.FromSeconds(3);

    // Each zombie is checked every few ticks, a slice per tick, so the LOS raycasts don't all land on one tick.
    private const int TickBuckets = 8;
    private int _tick;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var bucket = _tick++ % TickBuckets;
        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ActiveNPCComponent, WaveSpawnedTagComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var htn))
        {
            if (uid.Id % TickBuckets == bucket)
                Tick(uid, htn, curTime);
        }
    }

    private void Tick(EntityUid uid, HTNComponent htn, TimeSpan curTime)
    {
        var bb = htn.Blackboard;

        if (!bb.TryGetValue<EntityUid>("Target", out var target, EntityManager)
            || !Exists(target)
            || _mobState.IsDead(target))
        {
            bb.Remove<MapCoordinates>(FSAIBlackboardKeys.AggroOrigin);
            bb.Remove<TimeSpan>(FSAIBlackboardKeys.TargetLastSeen);
            bb.Remove<TimeSpan>(FSAIBlackboardKeys.AggroGraceUntil);
            return;
        }

        // The objective is never given up on; leashing it made zombies drop the CCC partway there.
        if (bb.TryGetValue<EntityUid>(NPCBlackboard.CurrentOrderedTarget, out var objective, EntityManager)
            && objective == target)
            return;

        if (!bb.TryGetValue<MapCoordinates>(FSAIBlackboardKeys.AggroOrigin, out var aggroOrigin, EntityManager))
        {
            aggroOrigin = _transform.GetMapCoordinates(uid);
            bb.SetValue(FSAIBlackboardKeys.AggroOrigin, aggroOrigin);
            bb.SetValue(FSAIBlackboardKeys.TargetLastSeen, curTime);
            return;
        }

        var graced = bb.TryGetValue<TimeSpan>(FSAIBlackboardKeys.AggroGraceUntil, out var graceUntil, EntityManager)
                     && curTime < graceUntil;

        // Distance is free; only raycast when it doesn't already decide.
        var curPos = _transform.GetMapCoordinates(uid);
        if (!graced && (curPos.MapId != aggroOrigin.MapId
                        || (curPos.Position - aggroOrigin.Position).Length() > LeashDistance))
        {
            ClearTarget(htn, bb);
            return;
        }

        if (_examine.InRangeUnOccluded(uid, target, LeashDistance + 5f, null))
            bb.SetValue(FSAIBlackboardKeys.TargetLastSeen, curTime);

        if (graced)
            return;

        if (bb.TryGetValue<TimeSpan>(FSAIBlackboardKeys.TargetLastSeen, out var lastSeen, EntityManager)
            && curTime - lastSeen > LosTimeout)
        {
            ClearTarget(htn, bb);
        }
    }

    private void ClearTarget(HTNComponent htn, NPCBlackboard bb)
    {
        bb.Remove<EntityUid>("Target");
        bb.Remove<MapCoordinates>(FSAIBlackboardKeys.AggroOrigin);
        bb.Remove<TimeSpan>(FSAIBlackboardKeys.TargetLastSeen);
        bb.Remove<TimeSpan>(FSAIBlackboardKeys.AggroGraceUntil);
        _htn.Replan(htn);
    }
}
