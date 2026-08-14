using System.Numerics;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Physics.Components;

namespace Content.Server._FinalStand.Upgrades.Effects;

// per-weapon shot counter for OverloadRoundUpgradeSystem
[RegisterComponent]
public sealed partial class FSOverloadRoundComponent : Component
{
    public int ShotCounter;
}

// every Nth shot (interval shrinks per level) is replaced with a visually distinct bolt dealing double damage
public sealed partial class OverloadRoundUpgradeSystem : EntitySystem
{
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private const string OverloadBoltProto = "FSBulletLaserCarbineOverload";
    private const float FallbackBoltSpeed = 20f;

    public void HandleAmmoShot(EntityUid uid, FSWeaponUpgradeStateComponent state, AmmoShotEvent args)
    {
        if (state.OverloadRoundLevel <= 0 || args.FiredProjectiles.Count == 0)
            return;

        var interval = state.OverloadRoundLevel switch
        {
            1 => 5,
            2 => 4,
            _ => 3,
        };

        var counter = EnsureComp<FSOverloadRoundComponent>(uid);
        counter.ShotCounter++;

        if (counter.ShotCounter < interval)
            return;

        counter.ShotCounter = 0;

        foreach (var projUid in args.FiredProjectiles)
        {
            EntityUid? shooter = null;
            var damage = new DamageSpecifier();
            if (TryComp<ProjectileComponent>(projUid, out var origProj))
            {
                shooter = origProj.Shooter;
                damage = origProj.Damage * 2.0f;
            }

            // carry the Homing Bolts turn rate over - the original bolt is about to be deleted
            var turnRate = 0f;
            if (TryComp<FSHomingProjectileComponent>(projUid, out var origHoming))
                turnRate = origHoming.TurnRateDegrees;

            Vector2 dir;
            float speed;
            if (TryComp<PhysicsComponent>(projUid, out var phys) && phys.LinearVelocity.LengthSquared() > 0.001f)
            {
                var vel = phys.LinearVelocity;
                speed = vel.Length();
                dir = vel / speed;
            }
            else
            {
                dir = _xform.GetWorldRotation(uid).ToVec();
                speed = FallbackBoltSpeed;
            }

            var spawnCoords = Transform(projUid).Coordinates;
            QueueDel(projUid);

            var bolt = Spawn(OverloadBoltProto, spawnCoords);
            if (TryComp<ProjectileComponent>(bolt, out var boltProj))
                boltProj.Damage = damage;

            if (turnRate > 0f)
                EnsureComp<FSHomingProjectileComponent>(bolt).TurnRateDegrees = turnRate;

            _gun.ShootProjectile(bolt, dir, Vector2.Zero, uid, shooter, speed);
        }
    }
}
