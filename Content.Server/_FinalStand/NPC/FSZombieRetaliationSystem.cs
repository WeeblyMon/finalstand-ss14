using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Effects;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.NPC;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.NPC;

public sealed partial class FSZombieRetaliationSystem : EntitySystem
{
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float RetaliationDuration = 2f;
    private const float AlertRadius = 10f;
    private const float AlertDuration = 1.5f;
    private const float AlertCooldown = 0.5f;
    // How much closer a new attacker must be before a retaliating zombie turns on them.
    private const float SwitchMargin = 2f;

    // Same rate FSBreachTargetSystem ticks at, kept in sync though the two accumulators are separate.
    private const float TickInterval = 0.1f;
    private float _accumulator;

    private readonly HashSet<Entity<WaveSpawnedTagComponent>> _peerBuffer = new();

    // One pack alert per attacker per area per cooldown. Keying it on the zombie that was hit meant a weapon
    // sweeping a dense horde ran a 10-tile lookup for every zombie it touched.
    private readonly FSEffectThrottle<(EntityUid Attacker, MapId Map, Vector2i Cell)> _alerts =
        new(TimeSpan.FromSeconds(AlertCooldown));

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _accumulator += frameTime;
        if (_accumulator < TickInterval) return;
        _accumulator -= TickInterval;

        // ActiveNPCComponent is dropped when a mob dies, so corpses never reach here.
        var query = EntityQueryEnumerator<ActiveNPCComponent, WaveSpawnedTagComponent, HTNComponent>();
        while (query.MoveNext(out _, out _, out _, out var htn))
            TickRetaliationTimer(htn, TickInterval);
    }

    // Owns the full lifecycle of RetaliationTimer/LastAttacker: TryRetaliate sets them, this decays them.
    private void TickRetaliationTimer(HTNComponent htn, float dt)
    {
        var bb = htn.Blackboard;

        if (bb.TryGetValue<float>(FSAIBlackboardKeys.RetaliationTimer, out var retTimer, EntityManager))
        {
            retTimer -= dt;
            if (retTimer <= 0f)
            {
                bb.Remove<float>(FSAIBlackboardKeys.RetaliationTimer);
                if (bb.ContainsKey(FSAIBlackboardKeys.LastAttacker))
                    bb.Remove<EntityUid>(FSAIBlackboardKeys.LastAttacker);
                StopRetaliating(htn);
                return;
            }
            bb.SetValue(FSAIBlackboardKeys.RetaliationTimer, retTimer);
        }

        if (bb.TryGetValue<EntityUid>(FSAIBlackboardKeys.LastAttacker, out var attacker, EntityManager)
            && (!Exists(attacker) || _mobState.IsDead(attacker)))
        {
            bb.Remove<EntityUid>(FSAIBlackboardKeys.LastAttacker);
            bb.Remove<float>(FSAIBlackboardKeys.RetaliationTimer);
            StopRetaliating(htn);
        }
    }

    // A retaliation plan outranks everything but a bait, so a replan would never replace it and the zombie kept
    // chasing the shooter after the timer ran out. Any other plan never read the keys and carries on.
    private void StopRetaliating(HTNComponent htn)
    {
        if (RunsRetaliationPlan(htn))
            _htn.AbortPlan(htn);
    }

    private static bool RunsRetaliationPlan(HTNComponent htn)
    {
        if (htn.Plan == null)
            return false;

        foreach (var task in htn.Plan.Tasks)
        {
            if (task.Operator is FSSetRetaliationTargetOperator)
                return true;
        }

        return false;
    }

    public void TryRetaliate(EntityUid uid, EntityUid attacker)
    {
        if (!HasComp<MobStateComponent>(attacker))
            return;
        if (!Exists(attacker) || _mobState.IsDead(attacker))
            return;
        if (HasComp<WaveSpawnedTagComponent>(attacker))
            return;
        if (!TryComp<HTNComponent>(uid, out var htn))
            return;

        var bb = htn.Blackboard;
        var curTime = _timing.CurTime;
        var newAttacker = !bb.TryGetValue<EntityUid>(FSAIBlackboardKeys.LastAttacker, out var previous, EntityManager)
                          || previous != attacker;

        bb.SetValue(FSAIBlackboardKeys.LastAttacker, attacker);
        bb.SetValue(FSAIBlackboardKeys.RetaliationTimer, RetaliationDuration);
        bb.SetValue(FSAIBlackboardKeys.AggroGraceUntil, curTime + TimeSpan.FromSeconds(RetaliationDuration));

        // Every bullet from the same gun only refreshes the timers; replanning is for a change of target. A running
        // retaliation plan took its target when it started and a replan never replaces a plan of the same rank, so
        // it is dropped instead, but only for an attacker clearly closer than the one being chased: two players
        // taking turns would otherwise restart the chase on every shot.
        if (RunsRetaliationPlan(htn))
        {
            if (bb.TryGetValue<EntityUid>("Target", out var chased, EntityManager)
                && chased != attacker
                && IsClearlyCloser(uid, attacker, chased))
            {
                _htn.AbortPlan(htn);
            }
        }
        else if (newAttacker)
        {
            _htn.Replan(htn);
        }

        bb.SetValue(FSAIBlackboardKeys.PackAlertCooldown, curTime + TimeSpan.FromSeconds(AlertCooldown));

        var pos = _transform.GetMapCoordinates(uid);
        if (_alerts.TryReserve((attacker, pos.MapId, (pos.Position / AlertRadius).Floored()), curTime))
            AlertNearby(uid, attacker);
    }

    private bool IsClearlyCloser(EntityUid uid, EntityUid attacker, EntityUid chased)
    {
        if (!Exists(chased))
            return true;

        var own = _transform.GetMapCoordinates(uid);
        var chasedPos = _transform.GetMapCoordinates(chased);
        if (chasedPos.MapId != own.MapId)
            return true;

        var attackerPos = _transform.GetMapCoordinates(attacker);
        if (attackerPos.MapId != own.MapId)
            return false;

        var attackerDist = (attackerPos.Position - own.Position).Length();
        var chasedDist = (chasedPos.Position - own.Position).Length();
        return attackerDist + SwitchMargin < chasedDist;
    }

    public bool Taunt(EntityUid zombie, EntityUid target, float seconds)
    {
        if (!TryComp<HTNComponent>(zombie, out var htn) || _mobState.IsDead(zombie))
            return false;

        var bb = htn.Blackboard;
        var hasExisting = bb.TryGetValue<EntityUid>(FSAIBlackboardKeys.LastAttacker, out var existing, EntityManager);
        if (hasExisting && existing != target && Exists(existing) && !_mobState.IsDead(existing))
            return false;

        bb.SetValue(FSAIBlackboardKeys.LastAttacker, target);
        bb.SetValue(FSAIBlackboardKeys.RetaliationTimer, seconds);
        bb.SetValue(FSAIBlackboardKeys.AggroGraceUntil, _timing.CurTime + TimeSpan.FromSeconds(seconds));

        // Taunting again only refreshes the timers, like another shot from the same attacker.
        if (!hasExisting || existing != target)
            _htn.Replan(htn);
        return true;
    }

    private void AlertNearby(EntityUid uid, EntityUid attacker)
    {
        _peerBuffer.Clear();
        _lookup.GetEntitiesInRange(Transform(uid).Coordinates, AlertRadius, _peerBuffer);

        foreach (var peer in _peerBuffer)
        {
            if (peer.Owner == uid || !HasComp<ActiveNPCComponent>(peer.Owner)) continue;
            if (!TryComp<HTNComponent>(peer.Owner, out var peerHtn)) continue;

            var bb = peerHtn.Blackboard;

            if (bb.TryGetValue<EntityUid>(FSAIBlackboardKeys.LastAttacker, out var existing, EntityManager)
                && Exists(existing) && !_mobState.IsDead(existing))
                continue;

            if (bb.TryGetValue<TimeSpan>(FSAIBlackboardKeys.PackAlertCooldown, out var cooldownEnd, EntityManager)
                && _timing.CurTime < cooldownEnd)
                continue;

            var dist = (_transform.GetWorldPosition(peer.Owner) - _transform.GetWorldPosition(uid)).Length();
            var attenuated = AlertDuration * (1f - dist / AlertRadius);
            if (attenuated <= 0f)
                continue;

            bb.SetValue(FSAIBlackboardKeys.LastAttacker, attacker);
            bb.SetValue(FSAIBlackboardKeys.RetaliationTimer, attenuated);
            bb.SetValue(FSAIBlackboardKeys.AggroGraceUntil, _timing.CurTime + TimeSpan.FromSeconds(attenuated));
            bb.SetValue(FSAIBlackboardKeys.PackAlertCooldown, _timing.CurTime + TimeSpan.FromSeconds(AlertCooldown));
            _htn.Replan(peerHtn);
        }
    }
}
