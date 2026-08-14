using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Upgrades.Effects;

// passive slow on every hit; no-refresh until expired; boss-immune via WaveSpawnedTagComponent check
public sealed partial class SuppressionUpgradeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    private static readonly TimeSpan SuppressionDuration = TimeSpan.FromSeconds(1.5);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
        SubscribeLocalEvent<FSSuppressionComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSSuppressionComponent>();
        while (query.MoveNext(out var uid, out var suppression))
        {
            if (now < suppression.EndTime)
                continue;
            RemComp<FSSuppressionComponent>(uid);
            _movement.RefreshMovementSpeedModifiers(uid);
        }
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (ev.State is not { } state || state.SuppressionLevel <= 0)
            return;
        if (!HasComp<WaveSpawnedTagComponent>(ev.Target))
            return;

        if (TryComp<FSSuppressionComponent>(ev.Target, out var existing) && existing.EndTime > _timing.CurTime)
            return;

        var suppression = EnsureComp<FSSuppressionComponent>(ev.Target);
        suppression.SlowFactor = state.SuppressionLevel == 1 ? 0.65f : 0.50f;
        suppression.EndTime = _timing.CurTime + SuppressionDuration;
        _movement.RefreshMovementSpeedModifiers(ev.Target);
    }

    private void OnRefreshSpeed(EntityUid uid, FSSuppressionComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(comp.SlowFactor, comp.SlowFactor);
    }
}
