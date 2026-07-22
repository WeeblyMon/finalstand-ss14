using Content.Server._FinalStand.Perks;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Movement.Systems;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed class FSSpeedDemonSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan DecayDelay = TimeSpan.FromSeconds(5);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSSpeedDemonComponent>();
        while (query.MoveNext(out var mindId, out var sd))
        {
            if (sd.Stacks <= 0) continue;
            if (now - sd.LastKillTime < DecayDelay) continue;

            sd.DecayAccumulator += frameTime;
            if (sd.DecayAccumulator < 1f) continue;

            sd.DecayAccumulator -= 1f;
            sd.Stacks--;
            SendStacksUpdate(mindId, sd.Stacks);
            if (TryComp<MindComponent>(mindId, out var mind) && mind.CurrentEntity.HasValue)
                _movement.RefreshMovementSpeedModifiers(mind.CurrentEntity.Value);
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead) return;
        if (!HasComp<FSZombieVisualsComponent>(args.Target)) return;
        if (!args.Origin.HasValue) return;
        if (!_mind.TryGetMind(args.Origin.Value, out var mindId, out _)) return;
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var augs)) return;

        var level = augs.GetSlottedLevel("SpeedDemon");
        if (level <= 0) return;

        var sd = EnsureComp<FSSpeedDemonComponent>(mindId);
        sd.Stacks = Math.Min(7, sd.Stacks + 1);
        sd.LastKillTime = _timing.CurTime;
        sd.DecayAccumulator = 0f;
        SendStacksUpdate(mindId, sd.Stacks);

        if (TryComp<MindComponent>(mindId, out var mind) && mind.CurrentEntity.HasValue)
            _movement.RefreshMovementSpeedModifiers(mind.CurrentEntity.Value);
    }

    private void SendStacksUpdate(EntityUid mindId, int stacks)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || !mind.CurrentEntity.HasValue) return;
        if (!TryComp<ActorComponent>(mind.CurrentEntity.Value, out var actor)) return;
        RaiseNetworkEvent(new FSPerkStacksUpdateEvent { PerkId = "SpeedDemon", Stacks = stacks },
            Filter.SinglePlayer(actor.PlayerSession));
    }
}
