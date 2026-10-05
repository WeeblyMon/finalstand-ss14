// Runs Skyfall missiles: no collision while they climb, then a dive onto the nearest zombie in the mech's sight, and a small splash.
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.Mech;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Mech;

public sealed partial class FSSkyfallSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private readonly HashSet<Entity<WaveSpawnedTagComponent>> _nearby = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSSkyfallLauncherComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<FSSkyfallLauncherComponent, AmmoShotEvent>(OnShot);
        SubscribeLocalEvent<FSSkyfallMissileComponent, ProjectileHitEvent>(OnHit);
        SubscribeLocalEvent<FSSkyfallMissileComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private int Live(EntityUid launcher)
    {
        var count = 0;
        var query = EntityQueryEnumerator<FSSkyfallMissileComponent>();
        while (query.MoveNext(out var missile))
        {
            if (missile.Launcher == launcher)
                count++;
        }

        return count;
    }

    private void OnShotAttempted(Entity<FSSkyfallLauncherComponent> ent, ref ShotAttemptedEvent args)
    {
        if (Live(ent) >= ent.Comp.MaxLive)
            args.Cancel();
    }

    private void OnShot(Entity<FSSkyfallLauncherComponent> ent, ref AmmoShotEvent args)
    {
        var shooter = CompOrNull<MechEquipmentComponent>(ent)?.EquipmentOwner;
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp<FSSkyfallMissileComponent>(projectile, out var missile))
                continue;

            missile.Launcher = ent;
            missile.Shooter = shooter;
            missile.Launched = _timing.CurTime;
            Dirty(projectile, missile);
        }
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSSkyfallMissileComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var missile, out var xform))
        {
            if (missile.Diving || now < missile.Launched + TimeSpan.FromSeconds(missile.Ascend))
                continue;

            missile.Diving = true;
            if (FindTarget(missile, xform) is not { } target)
                continue;

            var homing = EnsureComp<FSHomingProjectileComponent>(uid);
            homing.TurnRateDegrees = missile.TurnRate;
            homing.Target = target;
        }
    }

    private EntityUid? FindTarget(FSSkyfallMissileComponent missile, TransformComponent xform)
    {
        var here = _transform.GetMapCoordinates(xform);
        var shooter = missile.Shooter is { } s && Exists(s) ? s : (EntityUid?) null;
        var eye = shooter is { } from ? _transform.GetMapCoordinates(from) : here;
        SharedInteractionSystem.Ignored ignore = e => e == shooter || HasComp<MobStateComponent>(e);

        _nearby.Clear();
        _lookup.GetEntitiesInRange(here, missile.SearchRange, _nearby);

        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        foreach (var zombie in _nearby)
        {
            if (_mobState.IsDead(zombie))
                continue;

            var at = _transform.GetMapCoordinates(zombie);
            var distance = (at.Position - here.Position).LengthSquared();
            if (distance >= bestDistance || !_interaction.InRangeUnobstructed(eye, at, 0f, CollisionGroup.Opaque, ignore))
                continue;

            best = zombie;
            bestDistance = distance;
        }

        return best;
    }

    private void OnPreventCollide(Entity<FSSkyfallMissileComponent> ent, ref PreventCollideEvent args)
    {
        if (!ent.Comp.Diving)
            args.Cancelled = true;
    }

    private void OnHit(Entity<FSSkyfallMissileComponent> ent, ref ProjectileHitEvent args)
    {
        if (ent.Comp.SplashDamage.Empty)
            return;

        var hit = args.Target;
        _nearby.Clear();
        _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(ent), ent.Comp.SplashRadius, _nearby);
        foreach (var zombie in _nearby)
        {
            if (zombie.Owner != hit && !_mobState.IsDead(zombie))
                _damageable.TryChangeDamage(zombie.Owner, ent.Comp.SplashDamage, origin: ent.Comp.Shooter);
        }
    }
}
