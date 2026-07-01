using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.NPC;
using Content.Shared.Damage.Systems;
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
/// </summary>
public sealed class FSZombieRetaliationSystem : EntitySystem
{
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float RetaliationDuration = 2f;

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

        // TODO(finalstand): consider priority targeting (nearest vs last attacker).
        // Feed the Priority 1 retaliation branch (FinalStandPlayerRetaliationCompound) via its
        // gate keys. FSSetRetaliationTargetOperator will read FSLastAttacker and stamp Target
        // + TargetCoordinates during planning, so we don't touch Target directly here — that
        // avoids stepping on an ongoing MeleeService pursuit.
        htn.Blackboard.SetValue(FSAIBlackboardKeys.LastAttacker, attacker);
        htn.Blackboard.SetValue(FSAIBlackboardKeys.RetaliationTimer, RetaliationDuration);
        // Grace so FSLeashSystem doesn't immediately leash a zombie that was just shot.
        htn.Blackboard.SetValue("FSAggroGraceUntil", _timing.CurTime + TimeSpan.FromSeconds(RetaliationDuration));
        _htn.Replan(htn);
    }
}
