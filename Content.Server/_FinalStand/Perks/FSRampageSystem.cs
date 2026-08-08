using Content.Server._FinalStand.Leveling;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Movement.Systems;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed class FSRampageSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly FSPerkNotifySystem _notify = default!;

    private static readonly TimeSpan StackDecayInterval = TimeSpan.FromSeconds(2);

    private TimeSpan _nextDecayTick;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieKilledByPlayerEvent>(OnZombieKilled);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextDecayTick) return;
        _nextDecayTick = now + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<FSRampageComponent>();
        while (query.MoveNext(out var mindId, out var ramp))
        {
            if (ramp.Stacks <= 0) continue;
            if (now - ramp.LastKillTime < StackDecayInterval) continue;

            ramp.Stacks--;
            _notify.SendStacks(mindId, "Rampage", ramp.Stacks);
            if (TryComp<MindComponent>(mindId, out var mind) && mind.CurrentEntity.HasValue)
                _movement.RefreshMovementSpeedModifiers(mind.CurrentEntity.Value);
        }

        var regenQuery = EntityQueryEnumerator<FSRampageComponent, FSPerkLevelsComponent>();
        while (regenQuery.MoveNext(out var mindId, out var ramp, out var augs))
        {
            if (ramp.Stacks <= 0) continue;
            var level = augs.GetSlottedLevel("Rampage");
            if (level <= 0) continue;
            if (!TryComp<MindComponent>(mindId, out var mind) || !mind.CurrentEntity.HasValue) continue;

            _damageable.HealEvenly(mind.CurrentEntity.Value, FixedPoint2.New(-(ramp.Stacks * level * FSPerkBonusConstants.RampageRegenPerLevel)));
        }
    }

    private void OnZombieKilled(ref FSZombieKilledByPlayerEvent ev)
    {
        var level = ev.Perks.GetSlottedLevel("Rampage");
        if (level <= 0) return;

        var mindId = ev.MindId;
        var ramp = EnsureComp<FSRampageComponent>(mindId);
        ramp.Stacks = Math.Min(5, ramp.Stacks + 1);
        ramp.LastKillTime = _timing.CurTime;
        _notify.SendStacks(mindId, "Rampage", ramp.Stacks);

        if (TryComp<MindComponent>(mindId, out var mind) && mind.CurrentEntity.HasValue)
            _movement.RefreshMovementSpeedModifiers(mind.CurrentEntity.Value);
    }

}
