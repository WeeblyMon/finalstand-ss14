using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Weapons;

public sealed class FSRicochetSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // A cancelled collision leaves the pellet overlapping the wall for a moment, so without this it
    // would re-trigger every tick and burn its bounces instantly.
    private static readonly TimeSpan BounceCooldown = TimeSpan.FromSeconds(0.05);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRicochetComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<FSRicochetComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || ent.Comp.Bounces <= 0)
            return;

        if (HasComp<ProjectileComponent>(args.OtherEntity) || HasComp<MobStateComponent>(args.OtherEntity))
            return;

        if (!TryComp<PhysicsComponent>(args.OtherEntity, out var otherBody) ||
            otherBody.BodyType != BodyType.Static || !otherBody.Hard)
            return;

        var now = _timing.CurTime;
        if (now < ent.Comp.NextBounce)
            return;

        if (!TryComp<PhysicsComponent>(ent, out var body) || body.LinearVelocity.LengthSquared() < 0.01f)
            return;

        var sign = _random.Prob(0.5f) ? 1f : -1f;
        var angle = new Angle(MathHelper.DegreesToRadians(ent.Comp.BounceAngle) * sign);
        var deflected = angle.RotateVec(body.LinearVelocity);

        _physics.SetLinearVelocity(ent, deflected, body: body);
        _transform.SetWorldRotation(ent.Owner, deflected.ToWorldAngle());

        ent.Comp.Bounces--;
        ent.Comp.NextBounce = now + BounceCooldown;
        args.Cancelled = true;
    }
}
