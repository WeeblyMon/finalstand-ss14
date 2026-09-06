using Content.Server._FinalStand.Upgrades;
using Content.Server.Projectiles;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Mobs.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Weapons;

// budgets the bounces on a projectile the physics engine deflects for us
public sealed class FSRicochetSystem : EntitySystem
{
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public const string BounceFixture = "bounce";

    private static readonly TimeSpan BounceCooldown = TimeSpan.FromSeconds(0.05);
    private static readonly Angle FragmentSpread = Angle.FromDegrees(35);

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
            OnHitMob(ent, projectile, args.OtherEntity);
            return;
        }

        if (ent.Comp.Bounces > 0 || _timing.CurTime < ent.Comp.NextBounce)
            projectile.ProjectileSpent = false;
        else
            QueueDel(ent);
    }

    private void OnHitMob(Entity<FSRicochetComponent> ent, ProjectileComponent projectile, EntityUid target)
    {
        if (ent.Comp.Crit && !ent.Comp.Hit.Add(target))
            projectile.Damage *= ent.Comp.CritMultiplier;

        if (ent.Comp.Refund)
            Refund(ent, projectile);

        if (!HasComp<FSPierceComponent>(ent))
            QueueDel(ent);
    }

    private void Refund(Entity<FSRicochetComponent> ent, ProjectileComponent projectile)
    {
        if (projectile.Weapon is not { } gun || TerminatingOrDeleted(gun))
            return;

        if (!TryComp<BatteryAmmoProviderComponent>(gun, out var provider) || !HasComp<BatteryComponent>(gun))
            return;

        _battery.ChangeCharge(gun, provider.FireCost * ent.Comp.RefundFraction);
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

        if (ent.Comp.Fracture)
        {
            Fracture(ent, projectile);
            return;
        }

        projectile.DeleteOnCollide = true;
        _fixtures.DestroyFixture(ent.Owner, BounceFixture);
    }

    private void Fracture(Entity<FSRicochetComponent> ent, ProjectileComponent projectile)
    {
        if (MetaData(ent).EntityPrototype?.ID is not { } proto)
            return;

        var coords = Transform(ent).Coordinates;
        var hasBody = TryComp<PhysicsComponent>(ent, out var body);
        var speed = hasBody ? body!.LinearVelocity.Length() : 20f;
        var heading = hasBody && body!.LinearVelocity.LengthSquared() > 0.01f
            ? body.LinearVelocity.ToWorldAngle()
            : Angle.Zero;

        QueueDel(ent);

        for (var i = 0; i < ent.Comp.FragmentCount; i++)
        {
            var fan = ent.Comp.FragmentCount == 1
                ? 0f
                : (i / (float) (ent.Comp.FragmentCount - 1) - 0.5f) * 2f;
            var offset = new Angle(FragmentSpread.Theta * fan);

            var fragment = Spawn(proto, coords);
            RemComp<FSRicochetComponent>(fragment);
            RemComp<FSPierceComponent>(fragment);

            if (TryComp<ProjectileComponent>(fragment, out var fragProj))
            {
                fragProj.Damage = projectile.Damage * ent.Comp.FragmentDamage;
                fragProj.Shooter = projectile.Shooter;
                fragProj.Weapon = projectile.Weapon;
                fragProj.IgnoreShooter = true;
            }

            var direction = (heading + offset).ToWorldVec();
            _transform.SetWorldRotation(fragment, direction.ToWorldAngle());

            if (TryComp<PhysicsComponent>(fragment, out var fragBody))
            {
                _physics.SetBodyStatus(fragment, fragBody, BodyStatus.InAir);
                _physics.SetLinearVelocity(fragment, direction * speed, body: fragBody);
            }

            EnsureComp<TimedDespawnComponent>(fragment).Lifetime = ent.Comp.FragmentLifetime;
        }
    }
}
