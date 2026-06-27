using System.Numerics;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server._FinalStand.Upgrades.Effects;

// on hit, fires 3 splinters forward through the target in a cone for 40% damage each
public sealed class SplinterImpactUpgradeSystem : EntitySystem
{
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const int SplinterCount = 3;
    private const float SplinterConeHalfAngle = 0.26f; // ~15 degrees each side = 30 degree cone
    private const float SplinterDamageRatio = 0.4f;
    private const float SplinterSpeed = 12f;
    private const string SplinterProto = "FSPelletShotgunSplinter";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null || ev.Shooter == null)
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(ev.Weapon.Value, out var state) || !state.SplinterImpactEnabled)
            return;

        var targetPos = _xform.GetWorldPosition(ev.Target);
        var shooterPos = _xform.GetWorldPosition(ev.Shooter.Value);
        var toTarget = targetPos - shooterPos;
        if (toTarget.LengthSquared() < 0.001f)
            return;
        var forward = Vector2.Normalize(toTarget);
        var baseAngle = MathF.Atan2(forward.Y, forward.X);

        var targetCoords = Transform(ev.Target).Coordinates;
        var splinterDamage = ev.Damage * SplinterDamageRatio;

        for (var i = 0; i < SplinterCount; i++)
        {
            // Spread evenly across the cone: -half, 0, +half.
            var t = SplinterCount == 1 ? 0f : ((float)i / (SplinterCount - 1) - 0.5f) * 2f;
            var spreadAngle = baseAngle + t * SplinterConeHalfAngle;
            var dir = new Vector2(MathF.Cos(spreadAngle), MathF.Sin(spreadAngle));

            var splinter = Spawn(SplinterProto, targetCoords);

            // Override damage to 40% of primary hit.
            if (TryComp<Content.Shared.Projectiles.ProjectileComponent>(splinter, out var splinterProj))
                splinterProj.Damage = splinterDamage;
            _gun.ShootProjectile(splinter, dir, Vector2.Zero, ev.Shooter.Value, ev.Weapon, SplinterSpeed);
        }
    }
}
