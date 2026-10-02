using Content.Server.Destructible;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;

namespace Content.Server._FinalStand.Repair;

// Rivets patch the first fortification they hit instead of damaging it.
public sealed partial class FSRivetSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DestructibleSystem _destructible = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRivetComponent, ProjectileHitEvent>(OnRivetHit);
    }

    private void OnRivetHit(Entity<FSRivetComponent> ent, ref ProjectileHitEvent args)
    {
        if (!HasComp<FSPlateRepairComponent>(args.Target)
            || !TryComp<DamageableComponent>(args.Target, out var damageable))
            return;

        args.Damage = new DamageSpecifier();

        if (!_destructible.TryGetDestroyedAt(args.Target, out var max))
            return;

        var floor = max.Value.Float() * (1f - ent.Comp.RepairCap);
        var heal = MathF.Min(ent.Comp.Heal, damageable.TotalDamage.Float() - floor);
        if (heal > 0f)
            _damageable.HealEvenly((args.Target, damageable), -FixedPoint2.New(heal), origin: args.Shooter);
    }
}
