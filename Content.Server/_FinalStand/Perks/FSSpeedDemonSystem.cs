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

public sealed partial class FSSpeedDemonSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FSPerkNotifySystem _notify = default!;

    private static readonly TimeSpan DecayDelay = TimeSpan.FromSeconds(5);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieKilledByPlayerEvent>(OnZombieKilled);
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
            _notify.SendStacks(mindId, "SpeedDemon", sd.Stacks);
            if (TryComp<MindComponent>(mindId, out var mind) && mind.CurrentEntity.HasValue)
                _movement.RefreshMovementSpeedModifiers(mind.CurrentEntity.Value);
        }
    }

    private void OnZombieKilled(ref FSZombieKilledByPlayerEvent ev)
    {
        var level = ev.Perks.GetSlottedLevel("SpeedDemon");
        if (level <= 0) return;

        var mindId = ev.MindId;
        var sd = EnsureComp<FSSpeedDemonComponent>(mindId);
        sd.Stacks = Math.Min(7, sd.Stacks + 1);
        sd.LastKillTime = _timing.CurTime;
        sd.DecayAccumulator = 0f;
        _notify.SendStacks(mindId, "SpeedDemon", sd.Stacks);

        if (TryComp<MindComponent>(mindId, out var mind) && mind.CurrentEntity.HasValue)
            _movement.RefreshMovementSpeedModifiers(mind.CurrentEntity.Value);
    }

}
