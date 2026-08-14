using System.Numerics;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage;
using Content.Shared.Explosion.Components;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Trigger.Components.Effects;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Mobs;

public sealed partial class FSArmouredDeflectSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PointLightSystem _pointLight = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedExplosionSystem _explosion = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnProjectileHit);
        SubscribeLocalEvent<FSArmouredDeflectComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<FSArmouredDeflectComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.GlowTimer <= 0f)
                continue;
            comp.GlowTimer -= frameTime;
            if (comp.GlowTimer <= 0f)
            {
                comp.IsGlowing = false;
                _pointLight.SetEnabled(uid, false);
                Dirty(uid, comp);
            }
        }
    }

    private void OnProjectileHit(FSProjectileHitEffectEvent ev)
    {
        if (!TryComp<FSArmouredDeflectComponent>(ev.Target, out var comp))
            return;
        if (ev.Shooter == null)
            return;
        if (!_random.Prob(comp.DeflectChance))
            return;

        // Multiplied, not assigned: zero still wins, without discarding other subscribers.
        ev.AdditionalMultiplier *= 0f;

        _audio.PlayPvs(comp.DeflectSound, ev.Target);
        comp.IsGlowing = true;
        comp.GlowTimer = FSArmouredDeflectComponent.GlowDuration;
        _pointLight.SetEnabled(ev.Target, true);
        Dirty(ev.Target, comp);

        // Physical projectile uses its own proto; hitscan falls back to a laser bolt.
        var proto = ev.ProjectileUid != null
            ? MetaData(ev.ProjectileUid.Value).EntityPrototype?.ID ?? "BulletLaser"
            : "BulletLaser";

        // Strip ExplodeOnTrigger from the original so it doesn't blow up on the zombie.
        if (ev.ProjectileUid != null)
            RemComp<ExplodeOnTriggerComponent>(ev.ProjectileUid.Value);

        var zombieCoords = _transform.GetMapCoordinates(ev.Target);
        var shooterPos = _transform.GetWorldPosition(ev.Shooter.Value);
        var dir = Vector2.Normalize(shooterPos - zombieCoords.Position);

        var reflected = Spawn(proto, zombieCoords);

        // Reflected explosives use FSReflectedExplosion so FSExplosionFilterSystem doesn't block player damage.
        if (TryComp<ExplosiveComponent>(reflected, out var explosive))
        {
            _explosion.SetExplosionType(reflected, "FSReflectedExplosion");
            _explosion.SetTotalIntensity(reflected, explosive.TotalIntensity * 2f, explosive);
            _explosion.SetMaxIntensity(reflected, explosive.MaxIntensity * 2f, explosive);
        }

        if (TryComp<ProjectileComponent>(reflected, out var projComp))
        {
            projComp.Damage = new DamageSpecifier();
            foreach (var (type, amount) in ev.Damage.DamageDict)
                projComp.Damage.DamageDict[type] = amount * 2;
            projComp.Shooter = ev.Target;
            projComp.IgnoreShooter = true;
        }

        if (TryComp<PhysicsComponent>(reflected, out var body))
        {
            _physics.SetBodyStatus(reflected, body, BodyStatus.InAir);
            _physics.SetLinearVelocity(reflected, dir * 25f, body: body);
        }
    }

    private void OnShutdown(EntityUid uid, FSArmouredDeflectComponent comp, ComponentShutdown args)
    {
        if (comp.GlowTimer > 0f)
            _pointLight.SetEnabled(uid, false);
    }
}
