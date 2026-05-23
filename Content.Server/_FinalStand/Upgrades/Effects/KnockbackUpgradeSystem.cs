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

    private static readonly float[] VelocityByLevel = [5f, 9f, 14f];
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

        var level = Math.Clamp(state.KnockbackLevel, 1, VelocityByLevel.Length) - 1;
        var speed    = VelocityByLevel[level];
        var duration = DurationByLevel[level];

        var shooterPos = _transform.GetWorldPosition(ev.Shooter.Value);
        var targetPos  = _transform.GetWorldPosition(ev.Target);
        var dir = targetPos - shooterPos;
        if (dir == Vector2.Zero)
            return;

        var velocity = Vector2.Normalize(dir) * speed;

        // Already knocked back: redirect only, don't extend the timer (rapid-fire would freeze permanently).
        if (HasComp<FSKnockedBackComponent>(ev.Target))
        {
            _physics.SetLinearVelocity(ev.Target, velocity);
            return;
        }

        // Remove InputMoverComponent so the NPC mover can't override velocity. Restored in Update().
        var hadMover = HasComp<InputMoverComponent>(ev.Target);
        if (hadMover)
            RemComp<InputMoverComponent>(ev.Target);

        _physics.SetLinearVelocity(ev.Target, velocity);

        var comp = EnsureComp<FSKnockedBackComponent>(ev.Target);
        comp.EndTime = _timing.CurTime + duration;
        comp.InputMoverRemoved = hadMover;
    }
}
