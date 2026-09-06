using System.Numerics;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage;
using Content.Shared.Explosion.Components;
using Content.Shared.Projectiles;
using Content.Shared.Trigger.Components.Effects;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server._FinalStand.Mobs;

// glowing stance reflects incoming fire and sprays it back as shrapnel
public sealed partial class FSArmouredDeflectSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PointLightSystem _pointLight = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float ShrapnelSpeed = 18f;

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
            if (comp.PhaseTimer < 0f)
            {
                comp.PhaseTimer = _random.NextFloat(0f, comp.StartJitter);
                continue;
            }

            comp.PhaseTimer -= frameTime;
            if (comp.PhaseTimer > 0f)
                continue;

            comp.IsGlowing = !comp.IsGlowing;
            comp.PhaseTimer = comp.IsGlowing ? comp.StanceDuration : comp.VulnerableDuration;
            _pointLight.SetEnabled(uid, comp.IsGlowing);
            Dirty(uid, comp);
        }
    }

    private void OnProjectileHit(FSProjectileHitEffectEvent ev)
    {
        if (!TryComp<FSArmouredDeflectComponent>(ev.Target, out var comp))
            return;
        if (!comp.IsGlowing)
            return;

        ev.AdditionalMultiplier *= 0f;

        _audio.PlayPvs(comp.DeflectSound, ev.Target);

        if (ev.ProjectileUid != null)
            RemComp<ExplodeOnTriggerComponent>(ev.ProjectileUid.Value);

        SprayShrapnel(ev.Target, comp, ev.Damage);
    }

    private void SprayShrapnel(EntityUid zombie, FSArmouredDeflectComponent comp, DamageSpecifier damage)
    {
        if (comp.ShrapnelCount <= 0)
            return;

        var coords = _transform.GetMapCoordinates(zombie);
        var lifetime = comp.ShrapnelRange / ShrapnelSpeed;
        var spin = _random.NextFloat(0f, MathF.Tau);

        for (var i = 0; i < comp.ShrapnelCount; i++)
        {
            var angle = spin + MathF.Tau * i / comp.ShrapnelCount;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

            var piece = Spawn(comp.ShrapnelProto, coords);

            if (TryComp<ProjectileComponent>(piece, out var proj))
            {
                proj.Damage = new DamageSpecifier();
                foreach (var (type, amount) in damage.DamageDict)
                    proj.Damage.DamageDict[type] = amount * comp.ShrapnelDamageFraction;

                proj.Shooter = zombie;
                proj.IgnoreShooter = true;
            }


            _transform.SetWorldRotation(piece, dir.ToWorldAngle() + (proj?.Angle ?? Angle.Zero));

            if (TryComp<PhysicsComponent>(piece, out var body))
            {
                _physics.SetBodyStatus(piece, body, BodyStatus.InAir);
                _physics.SetLinearVelocity(piece, dir * ShrapnelSpeed, body: body);
            }

            var despawn = EnsureComp<TimedDespawnComponent>(piece);
            despawn.Lifetime = lifetime;
        }
    }

    private void OnShutdown(EntityUid uid, FSArmouredDeflectComponent comp, ComponentShutdown args)
    {
        if (comp.IsGlowing)
            _pointLight.SetEnabled(uid, false);
    }
}
