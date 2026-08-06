using System.Numerics;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Physics.Components;

namespace Content.Server._FinalStand.Upgrades.Effects;

// carries bolt damage + weapon/shooter so the shard cone on impact knows who fired it
[RegisterComponent]
public sealed partial class FSOverchargeBoltComponent : Component
{
    public DamageSpecifier BoltDamage = new();
    public EntityUid? Weapon;
    public EntityUid? Shooter;
}

// every 3rd shot replaces the normal spread with a big plasma bolt that bursts into a forward shard cone on hit
public sealed class OverchargeShotUpgradeSystem : EntitySystem
{
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const string BoltProto = "FSOverchargeBolt";
    private const string ShardProto = "FSOverchargeShard";
    private const float BoltSpeed = 35f;
    private const float ShardSpeed = 15f;
    private const int ShardCount = 6;
    private const float ShardConeHalfAngle = 0.39f;
    private const float ShardDamageRatio = 0.3f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSOverchargeBoltComponent, ProjectileHitEvent>(OnBoltHit);
    }

    public bool HandleAmmoShot(EntityUid uid, FSWeaponUpgradeStateComponent state, AmmoShotEvent args)
    {
        if (!state.OverchargeShotEnabled || args.FiredProjectiles.Count == 0)
            return false;

        var overcharge = EnsureComp<FSOverchargeComponent>(uid);
        overcharge.ShotCounter++;

        if (overcharge.ShotCounter % FSOverchargeComponent.ShotsPerCycle != 0)
            return false;

        var firstProj = args.FiredProjectiles[0];
        EntityUid? shooter = null;
        var pelletDamage = new DamageSpecifier();
        if (TryComp<ProjectileComponent>(firstProj, out var projComp))
        {
            shooter = projComp.Shooter;
            pelletDamage = projComp.Damage;
        }

        Vector2 dir;
        float speed;
        if (TryComp<PhysicsComponent>(firstProj, out var phys) && phys.LinearVelocity.LengthSquared() > 0.001f)
        {
            var vel = phys.LinearVelocity;
            speed = vel.Length();
            dir = vel / speed;
        }
        else
        {
            dir = _xform.GetWorldRotation(uid).ToVec();
            speed = BoltSpeed;
        }

        // Delete the normal pellets — overcharge replaces them entirely.
        foreach (var proj in args.FiredProjectiles)
            QueueDel(proj);

        // Spawn the overcharge bolt.
        var bolt = Spawn(BoltProto, Transform(uid).Coordinates);

        var boltDamage = new DamageSpecifier();
        if (TryComp<ProjectileComponent>(bolt, out var boltProj))
        {
            var perPellet = pelletDamage.GetTotal() > FixedPoint2.Zero ? pelletDamage : boltProj.Damage;
            boltDamage = perPellet * args.FiredProjectiles.Count;
            boltProj.Damage = boltDamage;
        }

        var boltComp = EnsureComp<FSOverchargeBoltComponent>(bolt);
        boltComp.BoltDamage = boltDamage;
        boltComp.Weapon = uid;
        boltComp.Shooter = shooter;

        _gun.ShootProjectile(bolt, dir, Vector2.Zero, uid, shooter, BoltSpeed);
        return true;
    }

    private void OnBoltHit(EntityUid uid, FSOverchargeBoltComponent comp, ref ProjectileHitEvent args)
    {
        // Get bolt travel direction from velocity or world rotation.
        Vector2 boltDir;
        if (TryComp<PhysicsComponent>(uid, out var phys) && phys.LinearVelocity.LengthSquared() > 0.001f)
        {
            var vel = phys.LinearVelocity;
            boltDir = vel / vel.Length();
        }
        else
        {
            boltDir = _xform.GetWorldRotation(uid).ToVec();
        }

        var boltPos = Transform(uid).Coordinates;
        var baseAngle = MathF.Atan2(boltDir.Y, boltDir.X);
        var shardDamage = comp.BoltDamage * ShardDamageRatio;

        for (var i = 0; i < ShardCount; i++)
        {
            var t = ShardCount == 1 ? 0f : ((float)i / (ShardCount - 1) - 0.5f) * 2f;
            var spreadAngle = baseAngle + t * ShardConeHalfAngle;
            var dir = new Vector2(MathF.Cos(spreadAngle), MathF.Sin(spreadAngle));

            var shard = Spawn(ShardProto, boltPos);
            if (TryComp<ProjectileComponent>(shard, out var shardProj))
                shardProj.Damage = shardDamage;

            _gun.ShootProjectile(shard, dir, Vector2.Zero, comp.Weapon ?? uid, comp.Shooter, ShardSpeed);
        }
    }
}
