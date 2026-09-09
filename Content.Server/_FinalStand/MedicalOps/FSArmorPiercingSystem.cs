using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

public sealed class FSArmorPiercingSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransformComponent, AmmoShotEvent>(OnAmmoShot);
    }

    private void OnAmmoShot(EntityUid uid, TransformComponent xform, AmmoShotEvent args)
    {
        var now = _timing.CurTime;

        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp<ProjectileComponent>(projectile, out var proj)
                || proj.Shooter is not { } shooter)
            {
                continue;
            }

            if (!TryComp<FSArmorPiercingComponent>(shooter, out var piercing) || piercing.Until <= now)
                continue;

            var flags = EnsureComp<FSProjectileFlagsComponent>(projectile);
            flags.Flags |= FinalStandDamageFlags.ArmorPenetrating;
        }
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<FSArmorPiercingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Until <= now)
                RemCompDeferred<FSArmorPiercingComponent>(uid);
        }
    }
}
