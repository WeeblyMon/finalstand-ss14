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

    // How long a target the leash gave up on is ignored by the hunt, long enough to walk back further than the chase went.
    private static readonly TimeSpan LeashIgnoreTime = TimeSpan.FromSeconds(5);

    // A target in view is only re-checked this often, so the recorded sighting can trail the real last one by
    // about this much; the timeout adds it back so ducking out of view still takes the full LosTimeout.
    private static readonly TimeSpan LosRecheck = TimeSpan.FromSeconds(1);

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
            if (FSTickBucket.Of(uid, TickBuckets) == bucket)
                Tick(uid, htn, curTime);
        }
    }

    private void Tick(EntityUid uid, HTNComponent htn, TimeSpan curTime)
    {
        var bb = htn.Blackboard;

        if (bb.TryGetValue<TimeSpan>(FSAIBlackboardKeys.LeashedUntil, out var leashedUntil, EntityManager)
            && curTime >= leashedUntil)
        {
            bb.Remove<EntityUid>(FSAIBlackboardKeys.LeashedTarget);
            bb.Remove<TimeSpan>(FSAIBlackboardKeys.LeashedUntil);
        }

        if (!bb.TryGetValue<EntityUid>("Target", out var target, EntityManager)
            || !Exists(target)
            || _mobState.IsDead(target))
        {
            bb.Remove<MapCoordinates>(FSAIBlackboardKeys.AggroOrigin);
            bb.Remove<TimeSpan>(FSAIBlackboardKeys.TargetLastSeen);
            bb.Remove<TimeSpan>(FSAIBlackboardKeys.AggroGraceUntil);
            return;
        }

        // The objective is never given up on; leashing it made zombies drop the CCC partway there. Nor are a bait
        // or a breach target, which have their own plans and aren't something to chase.
        if (IsKeyed(bb, NPCBlackboard.CurrentOrderedTarget, target)
            || IsKeyed(bb, FSAIBlackboardKeys.BaitTarget, target)
            || IsKeyed(bb, FSAIBlackboardKeys.BreachTarget, target))
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
            GiveUp(htn, bb, target, curTime);
            return;
        }

        var hasSeen = bb.TryGetValue<TimeSpan>(FSAIBlackboardKeys.TargetLastSeen, out var lastSeen, EntityManager);
        if ((!hasSeen || curTime - lastSeen >= LosRecheck)
            && _examine.InRangeUnOccluded(uid, target, LeashDistance + 5f, null))
        {
            hasSeen = true;
            lastSeen = curTime;
            bb.SetValue(FSAIBlackboardKeys.TargetLastSeen, curTime);
        }

        if (graced)
            return;

        if (hasSeen && curTime - lastSeen > LosTimeout + LosRecheck)
            GiveUp(htn, bb, target, curTime);
    }

    private bool IsKeyed(NPCBlackboard bb, string key, EntityUid target)
    {
        return bb.TryGetValue<EntityUid>(key, out var keyed, EntityManager) && keyed == target;
    }

    // The chase itself has to stop: a replan keeps the running plan unless the new one ranks higher, and only a bait
    // or retaliation outranks a chase, so the zombie kept following its target's live position.
    private void GiveUp(HTNComponent htn, NPCBlackboard bb, EntityUid target, TimeSpan curTime)
    {
        bb.Remove<EntityUid>("Target");
        bb.Remove<MapCoordinates>(FSAIBlackboardKeys.AggroOrigin);
        bb.Remove<TimeSpan>(FSAIBlackboardKeys.TargetLastSeen);
        bb.Remove<TimeSpan>(FSAIBlackboardKeys.AggroGraceUntil);
        bb.SetValue(FSAIBlackboardKeys.LeashedTarget, target);
        bb.SetValue(FSAIBlackboardKeys.LeashedUntil, curTime + LeashIgnoreTime);
        _htn.AbortPlan(htn);
    }
}
