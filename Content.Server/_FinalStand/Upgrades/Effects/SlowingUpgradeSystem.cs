using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Upgrades.Effects;

// Slows from different sources are tracked separately; the strongest active one applies.
public sealed partial class SlowingUpgradeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    private static readonly TimeSpan SlowDuration = TimeSpan.FromSeconds(2);
    private const float SlowFactor = 0.7f;

    private readonly List<string> _expired = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
        SubscribeLocalEvent<FSSlowedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    public void ApplySlow(EntityUid target, string source, float factor, TimeSpan duration)
    {
        var slow = EnsureComp<FSSlowedComponent>(target);
        slow.Slows[source] = (factor, _timing.CurTime + duration);
        Reapply(target, slow);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSSlowedComponent>();
        while (query.MoveNext(out var uid, out var slow))
        {
            foreach (var (source, entry) in slow.Slows)
            {
                if (now >= entry.EndTime)
                    _expired.Add(source);
            }

            if (_expired.Count == 0)
                continue;

            foreach (var source in _expired)
                slow.Slows.Remove(source);
            _expired.Clear();

            if (slow.Slows.Count == 0)
            {
                RemComp<FSSlowedComponent>(uid);
                _movement.RefreshMovementSpeedModifiers(uid);
                continue;
            }

            Reapply(uid, slow);
        }
    }

    private void Reapply(EntityUid uid, FSSlowedComponent slow)
    {
        var strongest = 1f;
        foreach (var (_, entry) in slow.Slows)
            strongest = MathF.Min(strongest, entry.Factor);

        if (MathHelper.CloseToPercent(strongest, slow.Applied))
            return;

        slow.Applied = strongest;
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (ev.State is not { } state || !state.SlowingEnabled)
            return;

        ApplySlow(ev.Target, "weapon", SlowFactor, SlowDuration);
    }

    private void OnRefreshSpeed(EntityUid uid, FSSlowedComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(comp.Applied, comp.Applied);
    }
}
