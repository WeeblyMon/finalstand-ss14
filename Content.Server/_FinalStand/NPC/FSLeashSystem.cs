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

    private float _accumulator;
    private const float TickInterval = 0.25f; // 4 Hz — cheap, LOS raycasts amortised

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < TickInterval)
            return;
        _accumulator -= TickInterval;

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ActiveNPCComponent, WaveSpawnedTagComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var htn))
            Tick(uid, htn, curTime);
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

        if (!bb.TryGetValue<MapCoordinates>(FSAIBlackboardKeys.AggroOrigin, out var aggroOrigin, EntityManager))
        {
            aggroOrigin = _transform.GetMapCoordinates(uid);
            bb.SetValue(FSAIBlackboardKeys.AggroOrigin, aggroOrigin);
            bb.SetValue(FSAIBlackboardKeys.TargetLastSeen, curTime);
            return;
        }

        if (_examine.InRangeUnOccluded(uid, target, LeashDistance + 5f, null))
            bb.SetValue(FSAIBlackboardKeys.TargetLastSeen, curTime);

        if (bb.TryGetValue<TimeSpan>(FSAIBlackboardKeys.AggroGraceUntil, out var graceUntil, EntityManager)
            && curTime < graceUntil)
            return;

        if (bb.TryGetValue<TimeSpan>(FSAIBlackboardKeys.TargetLastSeen, out var lastSeen, EntityManager)
            && curTime - lastSeen > LosTimeout)
        {
            ClearTarget(htn, bb);
            return;
        }

        var curPos = _transform.GetMapCoordinates(uid);
        if (curPos.MapId != aggroOrigin.MapId ||
            (curPos.Position - aggroOrigin.Position).Length() > LeashDistance)
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
