using Content.Server.RoundEnd;
using Content.Shared._FinalStand.CCC;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Station;

public sealed partial class FinalStandCCCSystem : EntitySystem
{
    [Dependency] private RoundEndSystem _roundEnd = default!;

    private float _broadcastCooldown;
    private const float BroadcastInterval = 0.5f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FinalStandCCCComponent, DestructionEventArgs>(OnCCCDestroyed);
        SubscribeLocalEvent<FinalStandCCCComponent, DamageChangedEvent>(OnCCCDamaged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_broadcastCooldown > 0f)
            _broadcastCooldown -= frameTime;
    }

    private void OnCCCDestroyed(EntityUid uid, FinalStandCCCComponent comp, DestructionEventArgs args)
    {
        Log.Info("[FinalStand] Central Command Console destroyed. Ending round.");
        _roundEnd.EndRound();
    }

    private void OnCCCDamaged(EntityUid uid, FinalStandCCCComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased) return;
        if (_broadcastCooldown > 0f) return;
        _broadcastCooldown = BroadcastInterval;
        RaiseNetworkEvent(new CCCUnderAttackEvent(), Filter.Broadcast());
    }
}
