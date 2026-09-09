using System.Numerics;
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
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Weapons;

// budgets the bounces on a projectile the physics engine deflects for us
public sealed class FSRicochetSystem : EntitySystem
{
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public const string BounceFixture = "bounce";

    private static readonly TimeSpan BounceCooldown = TimeSpan.FromSeconds(0.05);

    private readonly HashSet<EntityUid> _reorient = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRicochetComponent, StartCollideEvent>(OnStartCollide,
            after: [typeof(ProjectileSystem)], before: [typeof(FSPierceSystem)]);
        SubscribeLocalEvent<FSRicochetComponent, PreventCollideEvent>(OnPreventCollide);
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

    private void OnPreventCollide(Entity<FSRicochetComponent> ent, ref PreventCollideEvent args)
    {
        if (args.OurFixture.Hard && HasComp<MobStateComponent>(args.OtherEntity))
            args.Cancelled = true;
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
        {
            projectile.ProjectileSpent = false;
            return;
        }

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

        if (ent.Comp.Fracture)
            SpawnFragments(ent, projectile);

        if (ent.Comp.Bounces > 0)
            return;

        projectile.DeleteOnCollide = true;
        _fixtures.DestroyFixture(ent.Owner, BounceFixture);
    }

    private void SpawnFragments(Entity<FSRicochetComponent> ent, ProjectileComponent projectile)
    {
        var proto = ent.Comp.FragmentProto?.Id ?? MetaData(ent).EntityPrototype?.ID;
        if (proto == null || ent.Comp.FragmentCount <= 0)
            return;

        var coords = _transform.GetMapCoordinates(ent.Owner);
        var damage = projectile.Damage * ent.Comp.FragmentDamage;
        var spin = _random.NextFloat(0f, MathF.Tau);

        for (var i = 0; i < ent.Comp.FragmentCount; i++)
        {
            var angle = spin + MathF.Tau * i / ent.Comp.FragmentCount;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

            var fragment = Spawn(proto, coords);
            RemComp<FSPierceComponent>(fragment);

            if (ent.Comp.FragmentBounces > 0)
            {
                var fragBounce = EnsureComp<FSRicochetComponent>(fragment);
                fragBounce.Bounces = ent.Comp.FragmentBounces;
                fragBounce.DamageRetained = ent.Comp.DamageRetained;
                fragBounce.SpeedRetained = ent.Comp.SpeedRetained;
                fragBounce.BounceSound = ent.Comp.BounceSound;
                fragBounce.Fracture = false;
            }
            else
            {
                RemComp<FSRicochetComponent>(fragment);
                _fixtures.DestroyFixture(fragment, BounceFixture);
            }

            if (TryComp<ProjectileComponent>(fragment, out var fragProj))
            {
                fragProj.Damage = damage;
                fragProj.Shooter = projectile.Shooter;
                fragProj.Weapon = projectile.Weapon;
                fragProj.IgnoreShooter = true;
                fragProj.DeleteOnCollide = ent.Comp.FragmentBounces <= 0;
            }

            _transform.SetWorldRotation(fragment, direction.ToWorldAngle() + fragProj?.Angle ?? Angle.Zero);

            if (TryComp<PhysicsComponent>(fragment, out var fragBody))
            {
                _physics.SetBodyStatus(fragment, fragBody, BodyStatus.InAir);
                _physics.SetLinearVelocity(fragment, direction * ent.Comp.FragmentSpeed, body: fragBody);
            }

            EnsureComp<TimedDespawnComponent>(fragment).Lifetime = ent.Comp.FragmentLifetime;
        }
    }
}
