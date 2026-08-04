// Retaliates against off-screen attackers; alerts nearby allies with attenuated duration.
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.NPC;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.NPC;

public sealed class FSZombieRetaliationSystem : EntitySystem
{
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float RetaliationDuration = 2f;
    private const float AlertRadius = 10f;
    private const float AlertDuration = 1.5f;
    private const float AlertCooldown = 0.5f;

    private readonly HashSet<Entity<WaveSpawnedTagComponent>> _peerBuffer = new();

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
        bb.SetValue("FSAggroGraceUntil", curTime + TimeSpan.FromSeconds(RetaliationDuration));
        _htn.Replan(htn);

        // This fires once per damage instance, so an automatic weapon would otherwise run a
        // 10-tile query per bullet. The cooldown that already rate-limits each peer's alert
        // now also gates the broadcast itself.
        if (bb.TryGetValue<TimeSpan>("FSPackAlertCooldown", out var ownCooldown, EntityManager)
            && curTime < ownCooldown)
            return;

        bb.SetValue("FSPackAlertCooldown", curTime + TimeSpan.FromSeconds(AlertCooldown));
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

            if (bb.TryGetValue<TimeSpan>("FSPackAlertCooldown", out var cooldownEnd, EntityManager)
                && _timing.CurTime < cooldownEnd)
                continue;

            var dist = (_transform.GetWorldPosition(peer.Owner) - _transform.GetWorldPosition(uid)).Length();
            var attenuated = AlertDuration * (1f - dist / AlertRadius);
            if (attenuated <= 0f)
                continue;

            bb.SetValue(FSAIBlackboardKeys.LastAttacker, attacker);
            bb.SetValue(FSAIBlackboardKeys.RetaliationTimer, attenuated);
            bb.SetValue("FSAggroGraceUntil", _timing.CurTime + TimeSpan.FromSeconds(attenuated));
            bb.SetValue("FSPackAlertCooldown", _timing.CurTime + TimeSpan.FromSeconds(AlertCooldown));
            _htn.Replan(peerHtn);
        }
    }
}
