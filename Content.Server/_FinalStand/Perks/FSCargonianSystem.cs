using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

// Owns (FSCargonianBodyComponent, RefreshMovementSpeedModifiersEvent) — counters pulling drag penalty.
public sealed class FSCargonianSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    private static readonly float[] Counters = [1.018f, 1.034f, 1.053f, 1.053f];
    private static readonly TimeSpan SyncInterval = TimeSpan.FromSeconds(3);

    [Dependency] private readonly IGameTiming _timing = default!;
    private TimeSpan _nextSync;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSCargonianBodyComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        // Sync comp to all players with Cargonian slotted. Every-3s poll, same interval
        // FSOfficerSystem uses for its own slot-sync loop — OnRefreshSpeed below re-checks the
        // real slot state live regardless, so this only gates whether the event handler runs at
        // all, not correctness. No need to run it every tick.
        var now = _timing.CurTime;
        if (now < _nextSync) return;
        _nextSync = now + SyncInterval;

        var query = EntityQueryEnumerator<FSPerkLevelsComponent, MindComponent>();
        while (query.MoveNext(out var mindId, out var augs, out var mind))
        {
            if (!mind.CurrentEntity.HasValue) continue;
            var body = mind.CurrentEntity.Value;
            var level = augs.GetSlottedLevel("Cargonian");
            if (level > 0)
                EnsureComp<FSCargonianBodyComponent>(body);
            else
                RemCompDeferred<FSCargonianBodyComponent>(body);
        }
    }

    private void OnRefreshSpeed(EntityUid uid, FSCargonianBodyComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<PullerComponent>(uid, out var puller) || puller.Pulling == default) return;
        if (!_mind.TryGetMind(uid, out var mindId, out MindComponent? _)) return;
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var augs)) return;
        var level = augs.GetSlottedLevel("Cargonian");
        if (level <= 0) return;

        var counter = Counters[level - 1];
        args.ModifySpeed(counter, counter);
    }
}
