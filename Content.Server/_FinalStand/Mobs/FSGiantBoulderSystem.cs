using Content.Shared._FinalStand.Mobs;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;

namespace Content.Server._FinalStand.Mobs;

public sealed class FSGiantBoulderSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSGiantBoulderComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(Entity<FSGiantBoulderComponent> ent, ref StartCollideEvent args)
    {
        var other = args.OtherEntity;

        if (HasComp<MobStateComponent>(other) || HasComp<ProjectileComponent>(other))
            return;

        if (!TryComp<DamageableComponent>(other, out _) || !Transform(other).Anchored)
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict["Structural"] = FixedPoint2.New(ent.Comp.StructuralDamage);
        _damageable.TryChangeDamage(other, damage, ignoreResistances: true, origin: ent.Owner);

        Spawn(ent.Comp.ImpactEffect, Transform(other).Coordinates);

        ent.Comp.StructurePierce--;
        if (ent.Comp.StructurePierce > 0)
            return;

        Spawn(ent.Comp.ImpactEffect, Transform(ent).Coordinates);
        QueueDel(ent);
    }
}
