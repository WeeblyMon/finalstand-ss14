using Content.Server._FinalStand.Augments;
using Content.Shared._FinalStand.Augments;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Damage.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Server.Damage.Systems;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Augments;

public sealed class FSAdrenalineSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly float[] Durations = [2.1f, 2.8f, 3.5f, 4.2f];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSAdrenalineComponent, StaminaComponent>();
        while (query.MoveNext(out var uid, out var adr, out var stamina))
        {
            if (now >= adr.EndTime)
            {
                RemComp<FSAdrenalineComponent>(uid);
                SendTimerUpdate(uid, 0);
                continue;
            }

            _stamina.TakeStaminaDamage(uid, -(stamina.CritThreshold * frameTime * 10f));

            var secondsLeft = (int)Math.Ceiling((adr.EndTime - now).TotalSeconds);
            if (secondsLeft != adr.LastSentSeconds)
            {
                adr.LastSentSeconds = secondsLeft;
                SendTimerUpdate(uid, secondsLeft);
            }
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead) return;
        if (!HasComp<FSZombieVisualsComponent>(args.Target)) return;
        if (!args.Origin.HasValue) return;
        if (!_mind.TryGetMind(args.Origin.Value, out var mindId, out _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var augs)) return;

        var level = augs.GetSlottedLevel("Adrenaline");
        if (level <= 0) return;

        var duration = TimeSpan.FromSeconds(Durations[level - 1]);
        var newEnd = _timing.CurTime + duration;

        var adr = EnsureComp<FSAdrenalineComponent>(args.Origin.Value);
        // Don't let kills stack duration beyond the base — only refresh if almost expired.
        if (newEnd > adr.EndTime)
        {
            adr.EndTime = newEnd;
            adr.LastSentSeconds = -1;
        }
    }

    private void SendTimerUpdate(EntityUid bodyUid, int seconds)
    {
        if (!TryComp<ActorComponent>(bodyUid, out var actor)) return;
        RaiseNetworkEvent(new FSAugmentStacksUpdateEvent { AugId = "Adrenaline", Stacks = seconds },
            Filter.SinglePlayer(actor.PlayerSession));
    }
}
