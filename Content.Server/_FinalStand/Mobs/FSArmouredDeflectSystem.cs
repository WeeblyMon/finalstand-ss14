using System.Numerics;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Mobs;

public sealed class FSArmouredDeflectSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

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
        if (ev.ProjectileUid == null)
            return; // hitscan — no physical projectile to reflect
        if (!TryComp<FSArmouredDeflectComponent>(ev.Target, out var comp))
            return;
        if (ev.Shooter == null)
            return;
        if (!_random.Prob(comp.DeflectChance))
            return;

        ev.AdditionalMultiplier = 0f;

        // Sound and glow fire unconditionally once deflect is confirmed.
        _audio.PlayPvs(comp.DeflectSound, ev.Target);
        comp.IsGlowing = true;
        comp.GlowTimer = FSArmouredDeflectComponent.GlowDuration;
        _pointLight.SetEnabled(ev.Target, true);
        Dirty(ev.Target, comp);

        var proto = MetaData(ev.ProjectileUid.Value).EntityPrototype?.ID;
        if (proto == null)
            return;

        var zombieCoords = _transform.GetMapCoordinates(ev.Target);
        var shooterPos = _transform.GetWorldPosition(ev.Shooter.Value);
        var dir = Vector2.Normalize(shooterPos - zombieCoords.Position);

        var reflected = Spawn(proto, zombieCoords);

        if (TryComp<ProjectileComponent>(reflected, out var projComp))
        {
            projComp.Damage = new DamageSpecifier();
            foreach (var (type, amount) in ev.Damage.DamageDict)
                projComp.Damage.DamageDict[type] = amount;
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
