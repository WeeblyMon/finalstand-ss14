using System.Numerics;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed partial class KnockbackUpgradeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

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

            RemComp<FSKnockedBackComponent>(uid);
        }
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null || ev.Shooter == null)
            return;
        if (ev.State is not { } state || state.KnockbackLevel <= 0)
            return;

        ApplyKnockback(ev.Target, ev.Shooter.Value, state.KnockbackLevel, 1f + state.KnockbackResearchForceBonus);
    }

    public void ApplyKnockback(EntityUid target, EntityUid origin, int level, float forceMultiplier = 1f)
    {
        var idx = Math.Clamp(level, 1, VelocityByLevel.Length) - 1;
        var dir = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(origin);
        Push(target, dir, VelocityByLevel[idx] * forceMultiplier, DurationByLevel[idx]);
    }

    public void Push(EntityUid target, Vector2 direction, float speed, TimeSpan duration)
    {
        if (TryComp<FSKnockbackResistComponent>(target, out var resist))
            speed *= resist.Multiplier;

        if (speed <= 0f || direction == Vector2.Zero || HasComp<FSKnockedBackComponent>(target))
            return;

        if (!TryComp<PhysicsComponent>(target, out var body))
            return;

        _physics.SetLinearVelocity(target, body.LinearVelocity + Vector2.Normalize(direction) * speed, body: body);

        var comp = EnsureComp<FSKnockedBackComponent>(target);
        comp.EndTime = _timing.CurTime + duration;
        Dirty(target, comp);
    }
}
