using System.Linq;
using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Upgrades.Effects;

// on hit, proc chance to fire 3 beams toward random nearby enemies; beams deal 40% damage, no secondaries
public sealed class PrismaticUpgradeSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const string BeamProto = "FSPrismaticBeam";
    private const float BeamSpeed = 20f;
    private const float BeamRange = 10f;
    private const float BeamDamageRatio = 0.4f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null || ev.Shooter == null)
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(ev.Weapon.Value, out var state) || state.PrismaticLevel <= 0)
            return;

        if (!_random.Prob(state.PrismaticLevel * 0.25f))
            return;

        var targetPos = _xform.GetWorldPosition(ev.Target);
        var mapId = Transform(ev.Target).MapID;
        var nearby = new HashSet<Entity<WaveSpawnedTagComponent>>();
        _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(new MapCoordinates(targetPos, mapId), BeamRange, nearby);

        var targets = nearby
            .Where(e => e.Owner != ev.Target && !_mobState.IsDead(e.Owner))
            .Take(3)
            .ToList();

        var beamDamage = ev.Damage * FixedPoint2.New(BeamDamageRatio);
        var targetCoords = Transform(ev.Target).Coordinates;

        for (var i = 0; i < 3; i++)
        {
            Vector2 dir;
            if (i < targets.Count)
            {
                var enemyPos = _xform.GetWorldPosition(targets[i].Owner);
                var toEnemy = enemyPos - targetPos;
                if (toEnemy.LengthSquared() < 0.001f)
                    continue;
                dir = Vector2.Normalize(toEnemy);
            }
            else
            {
                var angle = _random.NextFloat() * MathF.Tau;
                dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }

            var beam = Spawn(BeamProto, targetCoords);
            if (TryComp<ProjectileComponent>(beam, out var proj))
                proj.Damage = beamDamage;

            _gun.ShootProjectile(beam, dir, Vector2.Zero, ev.Shooter.Value, null, BeamSpeed);
        }
    }
}
