// Boosts wave zombie speed at T-60s (1.25x) and T-30s (1.375x total); restores on wave end.
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.NPC;
using Content.Shared.GameTicking.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.NPC;

public sealed class FSWaveEnrageSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speedMod = default!;

    private readonly Dictionary<EntityUid, (bool T60, bool T30)> _applied = new();
    private readonly Dictionary<EntityUid, (float Walk, float Sprint)> _origSpeeds = new();

    private const float T60Multiplier = 1.25f;
    private const float T30TotalMultiplier = 1.375f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var ruleQuery = EntityQueryEnumerator<WaveGameRuleComponent, GameRuleComponent>();
        while (ruleQuery.MoveNext(out var ruleUid, out var rule, out _))
        {
            if (rule.Phase != WavePhase.Combat)
                continue;

            var remaining = (float)(rule.PhaseEndTime - _timing.CurTime).TotalSeconds;
            if (remaining <= 0f)
                continue;

            if (!_applied.TryGetValue(ruleUid, out var state))
                state = (false, false);

            if (!state.T60 && remaining <= 60f)
            {
                state.T60 = true;
                _applied[ruleUid] = state;
                ApplySpeedBoost(T60Multiplier);
            }

            if (!state.T30 && remaining <= 30f)
            {
                state.T30 = true;
                _applied[ruleUid] = state;
                ApplySpeedBoost(T30TotalMultiplier);
                ClearBreachCooldowns();
            }
        }
    }

    private void OnWaveEnded(ref WaveEndedEvent args)
    {
        _applied.Clear();
        RestoreSpeeds();
    }

    private void ApplySpeedBoost(float totalMultiplier)
    {
        var query = EntityQueryEnumerator<WaveSpawnedTagComponent, MovementSpeedModifierComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out _, out var move, out var htn))
        {
            if (!_origSpeeds.ContainsKey(uid))
                _origSpeeds[uid] = (move.BaseWalkSpeed, move.BaseSprintSpeed);

            var (origWalk, origSprint) = _origSpeeds[uid];
            _speedMod.ChangeBaseSpeed(uid, origWalk * totalMultiplier, origSprint * totalMultiplier, move.Acceleration, move);
            htn.Blackboard.SetValue("FSAggroGraceUntil", _timing.CurTime + TimeSpan.FromSeconds(90));
        }
    }

    private void ClearBreachCooldowns()
    {
        var query = EntityQueryEnumerator<WaveSpawnedTagComponent, HTNComponent>();
        while (query.MoveNext(out _, out _, out var htn))
            htn.Blackboard.SetValue(FSAIBlackboardKeys.BreachCooldown, TimeSpan.Zero);
    }

    private void RestoreSpeeds()
    {
        foreach (var (uid, (walk, sprint)) in _origSpeeds)
        {
            if (!Exists(uid))
                continue;
            if (!TryComp<MovementSpeedModifierComponent>(uid, out var move))
                continue;
            _speedMod.ChangeBaseSpeed(uid, walk, sprint, move.Acceleration, move);
        }
        _origSpeeds.Clear();
    }
}
