using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.HTN;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.NPC;

/// <summary>
/// Makes wave enemies retaliate against attackers that are outside their LOS.
/// The LOS check in NPCUtilitySystem correctly prevents vision-based detection through walls,
/// but that same check also blocks retaliation from off-screen shooters. When a wave enemy
/// takes damage, the attacker is written directly to the HTN "Target" blackboard key so
/// MeleeCombatCompound picks it up on the next replan, bypassing the vision check entirely.
/// </summary>
public sealed class FSZombieRetaliationSystem : EntitySystem
{
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

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

        // Only fill in the target when the zombie has nobody to chase — don't override
        // an active pursuit so a zombie mid-chase isn't redirected by a second shooter.
        if (htn.Blackboard.TryGetValue<EntityUid>("Target", out var current, EntityManager)
            && Exists(current)
            && !_mobState.IsDead(current))
            return;

        // TODO(finalstand): consider priority targeting (nearest vs last attacker)
        htn.Blackboard.SetValue("Target", attacker);
        // Stamp a grace period so FSLeashSystem doesn't immediately leash a zombie
        // that was just shot — it gets 2 seconds before the distance check activates.
        htn.Blackboard.SetValue("FSAggroGraceUntil", _timing.CurTime + TimeSpan.FromSeconds(2));
        _htn.Replan(htn);
    }
}
