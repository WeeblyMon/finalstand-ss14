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

// budgets the bounces on a projectile the physics engine deflects for us
public sealed class FSRicochetSystem : EntitySystem
{
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public const string BounceFixture = "bounce";

    private static readonly TimeSpan BounceCooldown = TimeSpan.FromSeconds(0.05);

    private readonly HashSet<EntityUid> _reorient = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRicochetComponent, StartCollideEvent>(OnStartCollide,
            after: [typeof(ProjectileSystem)], before: [typeof(FSPierceSystem)]);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_reorient.Count == 0)
            return;

        foreach (var uid in _reorient)
        {
            if (TerminatingOrDeleted(uid) || !TryComp<PhysicsComponent>(uid, out var body))
                continue;

            var velocity = body.LinearVelocity;
            if (velocity.LengthSquared() < 0.01f)
                continue;

            var facing = velocity.ToWorldAngle();
            if (TryComp<ProjectileComponent>(uid, out var projectile))
                facing += projectile.Angle;

            _transform.SetWorldRotation(uid, facing);
        }

        _reorient.Clear();
    }

    private void OnStartCollide(Entity<FSRicochetComponent> ent, ref StartCollideEvent args)
    {
        if (!TryComp<ProjectileComponent>(ent, out var projectile))
            return;

        if (args.OurFixtureId == BounceFixture)
        {
            OnBounced(ent, projectile);
            return;
        }

        if (args.OurFixtureId != SharedProjectileSystem.ProjectileFixture || !args.OtherFixture.Hard)
            return;

        if (HasComp<MobStateComponent>(args.OtherEntity))
        {
            if (!HasComp<FSPierceComponent>(ent))
                QueueDel(ent);
            return;
        }

        if (ent.Comp.Bounces > 0 || _timing.CurTime < ent.Comp.NextBounce)
            projectile.ProjectileSpent = false;
        else
            QueueDel(ent);
    }

    private void OnBounced(Entity<FSRicochetComponent> ent, ProjectileComponent projectile)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.NextBounce || ent.Comp.Bounces <= 0)
            return;

        ent.Comp.NextBounce = now + BounceCooldown;
        ent.Comp.Bounces--;

        projectile.Damage *= ent.Comp.DamageRetained;
        projectile.ProjectileSpent = false;

        _reorient.Add(ent.Owner);
        _audio.PlayPvs(ent.Comp.BounceSound, ent.Owner);

        if (ent.Comp.BounceEffect is { } effect)
            Spawn(effect, Transform(ent).Coordinates);

        if (ent.Comp.Bounces > 0)
            return;

        projectile.DeleteOnCollide = true;
        _fixtures.DestroyFixture(ent.Owner, BounceFixture);
    }
}
