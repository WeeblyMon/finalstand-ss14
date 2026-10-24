using Content.Server._FinalStand.Effects;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;

namespace Content.Server._FinalStand.Weapons;

public sealed partial class FSSplashShotSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FSFlairSystem _flair = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float RadiusPerBlastBonus = 0.5f;

    private readonly HashSet<Entity<WaveSpawnedTagComponent>> _nearby = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSSplashShotComponent, ProjectileHitEvent>(OnHit);
    }

    private void OnHit(Entity<FSSplashShotComponent> ent, ref ProjectileHitEvent args)
    {
        if (!TryComp<ProjectileComponent>(ent, out var projectile))
            return;

        var coords = Transform(ent).Coordinates;
        foreach (var burst in ent.Comp.Bursts)
            _flair.Burst(burst, coords);

        var radius = ent.Comp.Radius;
        if (projectile.Weapon is { } weapon && TryComp<FSWeaponUpgradeStateComponent>(weapon, out var state))
            radius += state.BlastRadiusBonus * RadiusPerBlastBonus;

        var damage = projectile.Damage * ent.Comp.Fraction;
        _nearby.Clear();
        _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(ent), radius, _nearby);

        foreach (var target in _nearby)
        {
            if (target.Owner == args.Target || _mobState.IsDead(target))
                continue;

            _damageable.TryChangeDamage(target.Owner, damage, origin: projectile.Shooter);
        }
    }
}
