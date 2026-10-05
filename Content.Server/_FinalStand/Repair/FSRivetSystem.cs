using Content.Server.Destructible;
using Content.Shared._FinalStand.Mech;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Mech.Components;
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

    private const float DefaultCap = 0.75f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRivetComponent, ProjectileHitEvent>(OnRivetHit);
    }

    private void OnRivetHit(Entity<FSRivetComponent> ent, ref ProjectileHitEvent args)
    {
        var planted = TryComp<FSSiegeModeComponent>(args.Target, out var siege) && siege.Planted;
        if (!HasComp<FSPlateRepairComponent>(args.Target) && !planted
            || !TryComp<DamageableComponent>(args.Target, out var damageable))
            return;

        args.Damage = new DamageSpecifier();

        TryComp<ProjectileComponent>(ent, out var projectile);
        if (projectile != null)
        {
            projectile.ImpactEffect = ent.Comp.RepairEffect;
            projectile.SoundHit = ent.Comp.RepairSound;
            projectile.ForceSound = true;
        }

        float max;
        if (TryComp<MechComponent>(args.Target, out var mech))
            max = mech.MaxIntegrity.Float();
        else if (_destructible.TryGetDestroyedAt(args.Target, out var destroyedAt))
            max = destroyedAt.Value.Float();
        else
            return;

        var cap = CompOrNull<FSRivetGunComponent>(projectile?.Weapon)?.RepairCap ?? DefaultCap;
        var floor = max * (1f - cap);
        var heal = MathF.Min(ent.Comp.Heal, damageable.TotalDamage.Float() - floor);
        if (heal > 0f)
            _damageable.HealEvenly((args.Target, damageable), -FixedPoint2.New(heal), origin: args.Shooter);
    }
}
