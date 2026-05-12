using Content.Shared._FinalStand.Shop;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Shop;

public sealed class FSWeaponUpgradeRuntimeSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, GunShotEvent>(OnGunShot);
    }

    private void OnGunShot(EntityUid uid, FSWeaponUpgradeStateComponent comp, ref GunShotEvent args)
    {
        foreach (var (ammoUid, _) in args.Ammo)
        {
            if (ammoUid == null)
                continue;

            if (!TryComp<ProjectileComponent>(ammoUid.Value, out var proj))
                continue;

            if (comp.PierceThreshold > FixedPoint2.Zero)
                proj.PenetrationThreshold += comp.PierceThreshold;

            if (comp.CritChance > 0f && _random.NextFloat() < comp.CritChance)
                proj.Damage *= comp.CritDamageMultiplier;
        }
    }
}
