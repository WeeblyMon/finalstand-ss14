using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed class SlowingUpgradeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    private static readonly TimeSpan SlowDuration = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
        SubscribeLocalEvent<FSSlowedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSSlowedComponent>();
        while (query.MoveNext(out var uid, out var slow))
        {
            if (now < slow.EndTime)
                continue;

            RemComp<FSSlowedComponent>(uid);
            _movement.RefreshMovementSpeedModifiers(uid);
        }
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (ev.State is not { } state || !state.SlowingEnabled)
            return;

        // Reset timer if already slowed; don't stack.
        var slow = EnsureComp<FSSlowedComponent>(ev.Target);
        slow.EndTime = _timing.CurTime + SlowDuration;
        _movement.RefreshMovementSpeedModifiers(ev.Target);
    }

    private void OnRefreshSpeed(EntityUid uid, FSSlowedComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(comp.SlowFactor, comp.SlowFactor);
    }
}
