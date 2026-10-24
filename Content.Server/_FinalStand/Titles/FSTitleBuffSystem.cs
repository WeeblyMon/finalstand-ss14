using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.Onboarding;
using Content.Shared._FinalStand.Titles;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Titles;

public sealed partial class FSTitleBuffSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;

    public FSTitleBuff ForBody(EntityUid? body)
    {
        return body is { } uid
               && TryComp<FSPlayerBadgeComponent>(uid, out var badge)
               && badge.Title is { } title
               && _proto.TryIndex(title, out var proto)
            ? proto.Buff
            : FSTitleBuff.None;
    }

    public FSTitleBuff ForMind(EntityUid mindId)
        => TryComp<MindComponent>(mindId, out var mind) ? ForBody(mind.CurrentEntity) : FSTitleBuff.None;

    public float DamageAgainst(EntityUid attacker, EntityUid target)
    {
        var buff = ForBody(attacker);
        var mult = buff.Damage;
        if (Math.Abs(buff.BossDamage - 1f) > 0.0001f && _waveRule.IsBoss(target))
            mult *= buff.BossDamage;
        if (Math.Abs(buff.EndlessDamage - 1f) > 0.0001f && _waveRule.IsEndless())
            mult *= buff.EndlessDamage;
        return mult;
    }

    public float HealingFor(EntityUid healer, EntityUid? target)
    {
        var buff = ForBody(healer);
        var mult = buff.Healing;
        if (target is { } patient && patient != healer && _mobState.IsCritical(patient))
            mult *= buff.DownedHealing;
        return mult;
    }

    public void Apply(EntityUid body)
    {
        _movement.RefreshMovementSpeedModifiers(body);

        var wanted = ForBody(body).MaxHealth;
        var bonus = EnsureComp<FSTitleHealthBonusComponent>(body);
        var delta = wanted - bonus.Applied;
        if (Math.Abs(delta) < 0.001f)
            return;

        foreach (var state in new[] { MobState.Critical, MobState.Dead })
        {
            if (_thresholds.TryGetThresholdForState(body, state, out var threshold))
                _thresholds.SetMobStateThreshold(body, threshold.Value + FixedPoint2.New(delta), state);
        }

        bonus.Applied = wanted;
    }
}
