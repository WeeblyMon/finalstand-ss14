using Content.Server.Destructible;
using Content.Server.RoundEnd;
using Content.Shared._FinalStand.CCC;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.CCC;

public sealed partial class FinalStandCCCSystem : EntitySystem
{
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan RoundEndCountdown = TimeSpan.FromSeconds(30);

    private TimeSpan _nextBroadcast;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FinalStandCCCComponent, MapInitEvent>(OnCCCMapInit);
        SubscribeLocalEvent<FinalStandCCCComponent, DestructionEventArgs>(OnCCCDestroyed);
        SubscribeLocalEvent<FinalStandCCCComponent, DamageChangedEvent>(OnCCCDamaged);
    }

    private void OnCCCMapInit(EntityUid uid, FinalStandCCCComponent comp, MapInitEvent args)
    {
        if (!TryComp<FinalStandCCCTagComponent>(uid, out var tag))
            return;

        tag.MaxHealth = _destructible.TryGetDestroyedAt(uid, out var destroyedAt)
            ? destroyedAt.Value.Float()
            : 0f;

        Dirty(uid, tag);
    }

    private void OnCCCDestroyed(EntityUid uid, FinalStandCCCComponent comp, DestructionEventArgs args)
    {
        Log.Info("[FinalStand] Central Command Console destroyed. Ending round.");
        _roundEnd.EndRound(RoundEndCountdown);
    }

    private void OnCCCDamaged(EntityUid uid, FinalStandCCCComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        var now = _timing.CurTime;
        if (now < _nextBroadcast)
            return;

        _nextBroadcast = now + BroadcastInterval;
        RaiseNetworkEvent(new CCCUnderAttackEvent(), Filter.Broadcast());
    }
}
