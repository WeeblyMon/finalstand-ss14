using System.Numerics;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Movement.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed class KnockbackUpgradeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly float[] VelocityByLevel = [5.5f, 9.9f, 15.4f];
    private static readonly TimeSpan[] DurationByLevel =
    [
        TimeSpan.FromSeconds(0.15),
        TimeSpan.FromSeconds(0.3),
        TimeSpan.FromSeconds(0.5),
    ];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSKnockedBackComponent>();
        while (query.MoveNext(out var uid, out var knockback))
        {
            if (now < knockback.EndTime)
                continue;

            if (knockback.InputMoverRemoved)
                EnsureComp<InputMoverComponent>(uid);
            RemComp<FSKnockedBackComponent>(uid);
        }
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null || ev.Shooter == null)
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(ev.Weapon.Value, out var state) || state.KnockbackLevel <= 0)
            return;

        ApplyKnockback(ev.Target, ev.Shooter.Value, state.KnockbackLevel, 1f + state.KnockbackResearchForceBonus);
    }

    public void ApplyKnockback(EntityUid target, EntityUid origin, int level, float forceMultiplier = 1f)
    {
        var idx = Math.Clamp(level, 1, VelocityByLevel.Length) - 1;
        var speed    = VelocityByLevel[idx] * forceMultiplier;
        var duration = DurationByLevel[idx];

        var originPos = _transform.GetWorldPosition(origin);
        var targetPos = _transform.GetWorldPosition(target);
        var dir = targetPos - originPos;
        if (dir == Vector2.Zero)
            return;

        var velocity = Vector2.Normalize(dir) * speed;

        // Already knocked back: redirect only, don't extend the timer.
        if (HasComp<FSKnockedBackComponent>(target))
        {
            _physics.SetLinearVelocity(target, velocity);
            return;
        }

        var hadMover = HasComp<InputMoverComponent>(target);
        if (hadMover)
            RemComp<InputMoverComponent>(target);

        _physics.SetLinearVelocity(target, velocity);

        var comp = EnsureComp<FSKnockedBackComponent>(target);
        comp.EndTime = _timing.CurTime + duration;
        comp.InputMoverRemoved = hadMover;
    }
}
