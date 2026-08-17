// Retaliates against off-screen attackers; alerts nearby allies with attenuated duration.
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.NPC;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
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

    // Same rate FSBreachTargetSystem ticks at, kept in sync though the two accumulators are separate.
    private const float TickInterval = 0.1f;
    private float _accumulator;

    private readonly HashSet<Entity<WaveSpawnedTagComponent>> _peerBuffer = new();

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
                return;
            }
            bb.SetValue(FSAIBlackboardKeys.RetaliationTimer, retTimer);
        }

        if (bb.TryGetValue<EntityUid>(FSAIBlackboardKeys.LastAttacker, out var attacker, EntityManager)
            && (!Exists(attacker) || _mobState.IsDead(attacker)))
        {
            bb.Remove<EntityUid>(FSAIBlackboardKeys.LastAttacker);
            bb.Remove<float>(FSAIBlackboardKeys.RetaliationTimer);
            _htn.Replan(htn);
        }
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
        bb.SetValue(FSAIBlackboardKeys.LastAttacker, attacker);
        bb.SetValue(FSAIBlackboardKeys.RetaliationTimer, RetaliationDuration);
        bb.SetValue(FSAIBlackboardKeys.AggroGraceUntil, curTime + TimeSpan.FromSeconds(RetaliationDuration));
        _htn.Replan(htn);

        // Gates the 10-tile broadcast query itself, not just each peer's alert, so full-auto weapons don't spam it.
        if (bb.TryGetValue<TimeSpan>(FSAIBlackboardKeys.PackAlertCooldown, out var ownCooldown, EntityManager)
            && curTime < ownCooldown)
            return;

        bb.SetValue(FSAIBlackboardKeys.PackAlertCooldown, curTime + TimeSpan.FromSeconds(AlertCooldown));
        AlertNearby(uid, attacker);
    }

    private void AlertNearby(EntityUid uid, EntityUid attacker)
    {
        _peerBuffer.Clear();
        _lookup.GetEntitiesInRange(Transform(uid).Coordinates, AlertRadius, _peerBuffer);

        foreach (var peer in _peerBuffer)
        {
            if (peer.Owner == uid) continue;
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
