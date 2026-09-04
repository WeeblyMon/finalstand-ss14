using System.Numerics;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Weapons;

public sealed class FSRicochetSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // A cancelled collision leaves the pellet overlapping the wall for a moment, so without this it
    // would re-trigger every tick and burn its bounces instantly.
    private static readonly TimeSpan BounceCooldown = TimeSpan.FromSeconds(0.05);

    // How far back out of the surface the pellet is nudged, so it leaves rather than tunnels through.
    private const float Clearance = 0.4f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRicochetComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<FSRicochetComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || ent.Comp.Bounces <= 0)
            return;

        // Whitelist rather than blacklist. Pellets carry a fly-by fixture layered Impassable, which
        // the projectile mask matches, so pellet-on-pellet contacts do get raised here. Nothing
        // unanchored can be a bounce surface, which rules them out whatever their body flags say.
        if (!IsBounceSurface(args.OtherEntity))
            return;

        var now = _timing.CurTime;
        if (now < ent.Comp.NextBounce)
            return;

        if (!TryComp<PhysicsComponent>(ent, out var body) || body.LinearVelocity.LengthSquared() < 0.01f)
            return;

        var pelletPos = _transform.GetWorldPosition(ent);
        var normal = SurfaceNormal(pelletPos - _transform.GetWorldPosition(args.OtherEntity));
        var velocity = body.LinearVelocity;

        // Mirror across the surface instead of rotating a fixed amount, so shallow hits graze and
        // head-on hits come straight back rather than veering off at a random angle.
        var deflected = velocity - 2f * Vector2.Dot(velocity, normal) * normal;
        if (deflected.LengthSquared() < 0.01f)
            deflected = -velocity;

        _physics.SetLinearVelocity(ent, deflected, body: body);
        _transform.SetWorldPosition(ent.Owner, pelletPos + normal * Clearance);
        _transform.SetWorldRotation(ent.Owner, deflected.ToWorldAngle());

        ent.Comp.Bounces--;
        ent.Comp.NextBounce = now + BounceCooldown;
        args.Cancelled = true;
    }

    private bool IsBounceSurface(EntityUid uid)
    {
        if (!Transform(uid).Anchored || HasComp<MobStateComponent>(uid))
            return false;

        return HasComp<DamageableComponent>(uid);
    }

    // Structures are tile aligned, so the face the pellet arrived at is whichever axis dominates.
    private static Vector2 SurfaceNormal(Vector2 offset)
    {
        if (offset.LengthSquared() < 0.0001f)
            return new Vector2(0f, 1f);

        return MathF.Abs(offset.X) >= MathF.Abs(offset.Y)
            ? new Vector2(MathF.Sign(offset.X), 0f)
            : new Vector2(0f, MathF.Sign(offset.Y));
    }
}
