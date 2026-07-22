using Content.Server._FinalStand.Perks;
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

    private static readonly TimeSpan StackDecayInterval = TimeSpan.FromSeconds(2);

    private TimeSpan _nextDecayTick;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
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
            SendStacksUpdate(mindId, ramp.Stacks);
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

            _damageable.HealEvenly(mind.CurrentEntity.Value, FixedPoint2.New(-(ramp.Stacks * level * 0.2f)));
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead) return;
        if (!HasComp<FSZombieVisualsComponent>(args.Target)) return;
        if (args.Origin == null) return;

        if (!_mind.TryGetMind(args.Origin.Value, out var mindId, out _)) return;
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var augs)) return;
        var level = augs.GetSlottedLevel("Rampage");
        if (level <= 0) return;

        var ramp = EnsureComp<FSRampageComponent>(mindId);
        ramp.Stacks = Math.Min(5, ramp.Stacks + 1);
        ramp.LastKillTime = _timing.CurTime;
        SendStacksUpdate(mindId, ramp.Stacks);

        if (TryComp<MindComponent>(mindId, out var mind) && mind.CurrentEntity.HasValue)
            _movement.RefreshMovementSpeedModifiers(mind.CurrentEntity.Value);
    }

    private void SendStacksUpdate(EntityUid mindId, int stacks)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || !mind.CurrentEntity.HasValue) return;
        if (!TryComp<ActorComponent>(mind.CurrentEntity.Value, out var actor)) return;
        RaiseNetworkEvent(new FSPerkStacksUpdateEvent { PerkId = "Rampage", Stacks = stacks },
            Filter.SinglePlayer(actor.PlayerSession));
    }
}
