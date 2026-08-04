using System.Numerics;
using Content.Server._FinalStand.Upgrades;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Upgrades.Effects;

// spawns extra pellets each shot; also called by FlechetteRoundsUpgradeSystem for the doubled set
public sealed class PelletCountUpgradeSystem : EntitySystem
{
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public void HandleAmmoShot(EntityUid uid, FSWeaponUpgradeStateComponent state, AmmoShotEvent args)
    {
        if (state.ExtraPellets <= 0 || args.FiredProjectiles.Count == 0)
            return;

        SpawnExtraPellets(uid, args.FiredProjectiles, state.ExtraPellets, addPierce: false);
    }

    public void SpawnExtraPellets(EntityUid gun, List<EntityUid> basePellets, int count, bool addPierce)
    {
        if (basePellets.Count == 0)
            return;

        var firstProj = basePellets[0];
        if (!TryComp<ProjectileComponent>(firstProj, out var projComp))
            return;

        var proto = MetaData(firstProj).EntityPrototype;
        if (proto == null)
            return;

        if (!TryComp<PhysicsComponent>(firstProj, out var physComp))
            return;

        var vel = physComp.LinearVelocity;
        var speed = vel.Length();
        if (speed < 0.001f)
            return;

        var baseDir = vel / speed;

        var spreadRad = TryComp<GunComponent>(gun, out var gunComp)
            ? (float)gunComp.MaxAngle.Theta
            : 0.26f; // ~15 degrees default

        var gunCoords = Transform(gun).Coordinates;

        for (var i = 0; i < count; i++)
        {
            var offset = (float)(_random.NextDouble() * 2.0 - 1.0) * spreadRad;
            var cosA = MathF.Cos(offset);
            var sinA = MathF.Sin(offset);
            var dir = new Vector2(
                baseDir.X * cosA - baseDir.Y * sinA,
                baseDir.X * sinA + baseDir.Y * cosA);

            var newProj = Spawn(proto.ID, gunCoords);
            _gun.ShootProjectile(newProj, dir, Vector2.Zero, gun, projComp.Shooter, speed);

            if (addPierce && TryComp<ProjectileComponent>(newProj, out var np))
            {
                np.DeleteOnCollide = false;
                var pierce = EnsureComp<FSPierceComponent>(newProj);
                pierce.RemainingPierces = Math.Max(pierce.RemainingPierces, 1);
            }

            if (TryComp<FSProjectileFlagsComponent>(firstProj, out var flags))
            {
                var newFlags = EnsureComp<FSProjectileFlagsComponent>(newProj);
                newFlags.Flags = flags.Flags;
                newFlags.ArmorShredMagnitude = flags.ArmorShredMagnitude;
            }

            if (TryComp<FSHomingProjectileComponent>(firstProj, out var homing))
                EnsureComp<FSHomingProjectileComponent>(newProj).TurnRateDegrees = homing.TurnRateDegrees;
        }
    }
}
