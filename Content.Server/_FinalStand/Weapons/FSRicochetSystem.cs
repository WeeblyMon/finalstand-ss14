using System.Numerics;
using Content.Server._FinalStand.Upgrades;
using Content.Server.Projectiles;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Weapons;

// Mirrors a projectile off whatever it just struck, using the contact manifold the physics engine
// already produced. Runs after ProjectileSystem so the hit is resolved, and before FSPierceSystem so
// clearing ProjectileSpent makes that system leave the round alone.
//
// Bouncing rounds are spawned with DeleteOnCollide off, so this system owns their lifetime: every
// path that does not bounce has to consume the round, or it sails on through the world.
public sealed class FSRicochetSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // One contact can report several manifold points in a tick; only the first should cost a bounce.
    private static readonly TimeSpan BounceCooldown = TimeSpan.FromSeconds(0.05);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRicochetComponent, StartCollideEvent>(OnStartCollide,
            after: [typeof(ProjectileSystem)], before: [typeof(FSPierceSystem)]);
    }

    private void OnStartCollide(Entity<FSRicochetComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != SharedProjectileSystem.ProjectileFixture)
            return;

        // Non-hard fixtures are what pellets present to each other, so this is the check that stops
        // a volley from ricocheting off itself.
        if (!args.OtherFixture.Hard)
            return;

        if (!TryComp<ProjectileComponent>(ent, out var projectile))
            return;

        // Mobs never deflect a round. FSPierceSystem decides whether it carries on through them;
        // without pierce left there is nothing else to end it here.
        if (HasComp<MobStateComponent>(args.OtherEntity))
        {
            if (!HasComp<FSPierceComponent>(ent))
                QueueDel(ent);
            return;
        }

        var now = _timing.CurTime;
        if (now < ent.Comp.NextBounce)
            return;

        if (ent.Comp.Bounces <= 0 || !TryComp<PhysicsComponent>(ent, out var body))
        {
            QueueDel(ent);
            return;
        }

        var velocity = body.LinearVelocity;
        if (velocity.LengthSquared() < 0.01f)
        {
            QueueDel(ent);
            return;
        }

        // The manifold normal is handed to both entities unflipped, so its sign depends on which
        // side of the contact we were. Orient it against our own travel instead of guessing from
        // positions, which is unreliable once we are already overlapping the surface.
        var normal = args.WorldNormal;
        if (Vector2.Dot(velocity, normal) > 0f)
            normal = -normal;

        var reflected = (velocity - 2f * Vector2.Dot(velocity, normal) * normal) * ent.Comp.SpeedRetained;

        _physics.SetLinearVelocity(ent, reflected, body: body);
        _transform.SetWorldPosition(ent.Owner,
            _transform.GetWorldPosition(ent) + normal * ent.Comp.Clearance);
        _transform.SetWorldRotation(ent.Owner, reflected.ToWorldAngle());

        projectile.Damage *= ent.Comp.DamageRetained;
        projectile.ProjectileSpent = false;

        ent.Comp.Bounces--;
        ent.Comp.NextBounce = now + BounceCooldown;

        // Out of bounces: let the next surface consume it the normal way.
        if (ent.Comp.Bounces <= 0)
            projectile.DeleteOnCollide = true;

        _audio.PlayPvs(ent.Comp.BounceSound, ent.Owner);

        if (ent.Comp.BounceEffect is { } effect)
            Spawn(effect, Transform(ent).Coordinates);
    }
}
