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
using Robust.Shared.Prototypes;
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

    private static readonly EntProtoId Blast = "FSSkyfallBlast";

    private readonly HashSet<Entity<WaveSpawnedTagComponent>> _nearby = new();
    private readonly List<(EntityUid Target, float Distance)> _visible = new();

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
        var owner = CompOrNull<MechEquipmentComponent>(ent)?.EquipmentOwner;
        var now = _timing.CurTime;
        var targeted = false;
        var index = 0;
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp<FSSkyfallMissileComponent>(projectile, out var missile))
                continue;

            missile.Launcher = ent;
            // The pilot fired it. The mech has no mind, so crediting it paid nobody for splash kills.
            missile.Shooter = CompOrNull<ProjectileComponent>(projectile)?.Shooter ?? owner;
            missile.Launched = now;

            // The whole salvo leaves from the same spot, so one search spreads it over the nearest targets.
            if (!targeted)
            {
                Visible(missile, Transform(projectile), args.FiredProjectiles.Count);
                targeted = true;
            }

            missile.LockedOn = _visible.Count > 0 ? _visible[index++ % _visible.Count].Target : null;
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
            var target = missile.LockedOn is { } locked && !TerminatingOrDeleted(locked) && !_mobState.IsDead(locked)
                ? locked
                : FindTarget(missile, xform);
            if (target != missile.LockedOn)
            {
                missile.LockedOn = target;
                Dirty(uid, missile);
            }

            if (target == null)
                continue;

            var homing = EnsureComp<FSHomingProjectileComponent>(uid);
            homing.TurnRateDegrees = missile.TurnRate;
            homing.Target = target.Value;
        }
    }

    private EntityUid? FindTarget(FSSkyfallMissileComponent missile, TransformComponent xform)
    {
        Visible(missile, xform, 1);
        return _visible.Count > 0 ? _visible[0].Target : null;
    }

    // Nearest first, raycasting only until there are enough; a raycast per zombie in range was most of a salvo's cost.
    private void Visible(FSSkyfallMissileComponent missile, TransformComponent xform, int wanted)
    {
        var here = _transform.GetMapCoordinates(xform);
        var shooter = missile.Shooter is { } s && Exists(s) ? s : (EntityUid?) null;
        var eye = shooter is { } from ? _transform.GetMapCoordinates(from) : here;
        SharedInteractionSystem.Ignored ignore = e => e == shooter || HasComp<MobStateComponent>(e);

        _nearby.Clear();
        _lookup.GetEntitiesInRange(here, missile.SearchRange, _nearby);

        _visible.Clear();
        foreach (var zombie in _nearby)
        {
            if (!_mobState.IsDead(zombie))
                _visible.Add((zombie, (_transform.GetWorldPosition(zombie) - here.Position).LengthSquared()));
        }

        _visible.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        var found = 0;
        for (var i = 0; i < _visible.Count && found < wanted; i++)
        {
            if (_interaction.InRangeUnobstructed(eye, _transform.GetMapCoordinates(_visible[i].Target), 0f, CollisionGroup.Opaque, ignore))
                _visible[found++] = _visible[i];
        }

        _visible.RemoveRange(found, _visible.Count - found);
    }

    private void OnPreventCollide(Entity<FSSkyfallMissileComponent> ent, ref PreventCollideEvent args)
    {
        if (!ent.Comp.Diving || !HasComp<WaveSpawnedTagComponent>(args.OtherEntity))
            args.Cancelled = true;
    }

    private void OnHit(Entity<FSSkyfallMissileComponent> ent, ref ProjectileHitEvent args)
    {
        Spawn(Blast, _transform.GetMapCoordinates(ent));
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
