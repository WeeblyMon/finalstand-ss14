using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.NPC;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.NPC;

/// <summary>
/// Makes wave enemies retaliate against attackers that are outside their LOS.
/// The LOS check in NPCUtilitySystem correctly prevents vision-based detection through walls,
/// but that same check also blocks retaliation from off-screen shooters. When a wave enemy
/// takes damage we seed the FS retaliation blackboard keys so the HTN's Priority 1
/// FinalStandPlayerRetaliationCompound branch fires on the next replan.
/// Pack alert: the directly-hit zombie's cluster also gets alerted, with duration
/// attenuated by distance so only nearby zombies join the pursuit.
/// </summary>
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

    // CritSystem owns WaveSpawnedTagComponent+DamageChangedEvent (one subscriber per pair
    // limit). It calls TryRetaliate directly after processing each damage event.
    public void TryRetaliate(EntityUid uid, EntityUid attacker)
    {
        // Skip non-mob sources (fire tiles, acid pools, etc. have no MobStateComponent).
        if (!HasComp<MobStateComponent>(attacker))
            return;

        if (!Exists(attacker) || _mobState.IsDead(attacker))
            return;

        // Don't retaliate against other wave enemies (zombie splash, xeno chain, etc.).
        if (HasComp<WaveSpawnedTagComponent>(attacker))
            return;

        if (!TryComp<HTNComponent>(uid, out var htn))
            return;

        htn.Blackboard.SetValue(FSAIBlackboardKeys.LastAttacker, attacker);
        htn.Blackboard.SetValue(FSAIBlackboardKeys.RetaliationTimer, RetaliationDuration);
        htn.Blackboard.SetValue("FSAggroGraceUntil", _timing.CurTime + TimeSpan.FromSeconds(RetaliationDuration));
        _htn.Replan(htn);

        AlertNearby(uid, attacker);
    }

    private void AlertNearby(EntityUid uid, EntityUid attacker)
    {
        var nearby = new HashSet<Entity<WaveSpawnedTagComponent>>();
        _lookup.GetEntitiesInRange(Transform(uid).Coordinates, AlertRadius, nearby);

        foreach (var peer in nearby)
        {
            if (peer.Owner == uid) continue;
            if (!TryComp<HTNComponent>(peer.Owner, out var peerHtn)) continue;

            var bb = peerHtn.Blackboard;

            // Don't override an active pursuit.
            if (bb.TryGetValue<EntityUid>(FSAIBlackboardKeys.LastAttacker, out var existing, EntityManager)
                && Exists(existing) && !_mobState.IsDead(existing))
                continue;

            // Rate-limit: a burst of shots shouldn't trigger a replan storm.
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
