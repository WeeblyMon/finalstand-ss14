using Content.Shared._FinalStand.Projectiles;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;

namespace Content.Server._FinalStand.Projectiles;

public sealed class FSNoStructureDamageSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSNoStructureDamageComponent, ProjectileHitEvent>(OnHit);
    }

    private void OnHit(EntityUid uid, FSNoStructureDamageComponent _, ref ProjectileHitEvent args)
    {
        if (!HasComp<MobStateComponent>(args.Target))
            args.Damage = new DamageSpecifier();
    }
}
