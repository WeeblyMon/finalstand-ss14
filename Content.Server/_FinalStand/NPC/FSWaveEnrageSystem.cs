// Boosts wave zombie speed once a wave's combat phase has run long: 1.25x after Stage1Elapsed,
// 1.375x total after Stage2Elapsed. Restores when combat ends.
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.NPC;
using Content.Shared.GameTicking.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.NPC;

public sealed partial class FSWaveEnrageSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MovementSpeedModifierSystem _speedMod = default!;

    private readonly Dictionary<EntityUid, (float Walk, float Sprint, int Stage)> _boosted = new();

    private const float Stage1Multiplier = 1.25f;
    private const float Stage2TotalMultiplier = 1.375f;
    private const float TickInterval = 1f;

    // Best-guess defaults, not tuned against real playtest telemetry.
    private static readonly TimeSpan Stage1Elapsed = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Stage2Elapsed = TimeSpan.FromMinutes(8);

    // 0 = not enraged, 1 = Stage1Elapsed reached, 2 = Stage2Elapsed reached.
    private int _stage;
    private float _accumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < TickInterval)
            return;
        _accumulator -= TickInterval;

        var stage = GetRuleStage();

        // Derived entirely from rule state, so any way a wave ends winds the boost back with no dedicated event.
        if (stage == 0)
        {
            if (_stage != 0)
            {
                _stage = 0;
                RestoreSpeeds();
            }
            return;
        }

        if (stage > _stage)
        {
            _stage = stage;
            if (stage == 2)
                ClearBreachCooldowns();
        }

        ApplyEnrage(_stage == 2 ? Stage2TotalMultiplier : Stage1Multiplier);
    }

    private int GetRuleStage()
    {
        var stage = 0;
        var query = EntityQueryEnumerator<WaveGameRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out _, out var rule, out _))
        {
            if (rule.Phase != WavePhase.Combat)
                continue;
            var remaining = rule.PhaseEndTime - _timing.CurTime;
            if (remaining <= TimeSpan.Zero)
                continue;
            var elapsed = rule.MaxCombatDuration - remaining;

            if (elapsed >= Stage2Elapsed)
                stage = Math.Max(stage, 2);
            else if (elapsed >= Stage1Elapsed)
                stage = Math.Max(stage, 1);
        }
        return stage;
    }

    private void ApplyEnrage(float totalMultiplier)
    {
        var query = EntityQueryEnumerator<ActiveNPCComponent, WaveSpawnedTagComponent, MovementSpeedModifierComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var move, out var htn))
        {
            if (_boosted.TryGetValue(uid, out var entry))
            {
                if (entry.Stage == _stage)
                    continue;
            }
            else
            {
                entry = (move.BaseWalkSpeed, move.BaseSprintSpeed, 0);
                htn.Blackboard.SetValue(FSAIBlackboardKeys.AggroGraceUntil, _timing.CurTime + TimeSpan.FromSeconds(90));
            }

            _speedMod.ChangeBaseSpeed(uid,
                entry.Walk * totalMultiplier,
                entry.Sprint * totalMultiplier,
                move.Acceleration,
                move);
            _boosted[uid] = (entry.Walk, entry.Sprint, _stage);
        }
    }

    // BreachCooldown is a float everywhere it is read; removing the key is what "clear" means.
    private void ClearBreachCooldowns()
    {
        var query = EntityQueryEnumerator<ActiveNPCComponent, WaveSpawnedTagComponent, HTNComponent>();
        while (query.MoveNext(out _, out _, out _, out var htn))
            htn.Blackboard.Remove<float>(FSAIBlackboardKeys.BreachCooldown);
    }

    private void RestoreSpeeds()
    {
        foreach (var (uid, (walk, sprint, _)) in _boosted)
        {
            if (!TryComp<MovementSpeedModifierComponent>(uid, out var move))
                continue;
            _speedMod.ChangeBaseSpeed(uid, walk, sprint, move.Acceleration, move);
        }
        _boosted.Clear();
    }
}
