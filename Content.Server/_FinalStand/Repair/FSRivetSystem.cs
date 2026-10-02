using Content.Server.Destructible;
using Content.Server.Popups;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Projectiles;
using Content.Shared.Stacks;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Repair;

// Rivets patch the first fortification they hit instead of damaging it, and are reloaded from steel sheets.
public sealed partial class FSRivetSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRivetGunComponent, InteractUsingEvent>(OnLoad);
        SubscribeLocalEvent<FSRivetComponent, ProjectileHitEvent>(OnRivetHit);
    }

    private void OnLoad(Entity<FSRivetGunComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled
            || !TryComp<StackComponent>(args.Used, out var stack)
            || stack.StackTypeId != ent.Comp.Material
            || !TryComp<BasicEntityAmmoProviderComponent>(ent, out var ammo)
            || ammo.Count is not { } count
            || ammo.Capacity is not { } capacity)
            return;

        args.Handled = true;
        var room = capacity - count;
        if (room <= 0)
        {
            _popup.PopupEntity(Loc.GetString("fs-rivet-gun-full"), ent, args.User);
            return;
        }

        if (!_stack.TryUse((args.Used, stack), 1))
            return;

        var added = Math.Min(room, ent.Comp.RivetsPerSheet);
        _gun.ChangeBasicEntityAmmoCount((ent, ammo), added);
        _popup.PopupEntity(Loc.GetString("fs-rivet-gun-loaded", ("count", added)), ent, args.User);
    }

    private void OnRivetHit(Entity<FSRivetComponent> ent, ref ProjectileHitEvent args)
    {
        if (!TryComp<FSPlateRepairComponent>(args.Target, out var repair)
            || !TryComp<DamageableComponent>(args.Target, out var damageable))
            return;

        args.Damage = new DamageSpecifier();

        if (_timing.CurTime - repair.LastDamaged < ent.Comp.HitCooldown
            || !_destructible.TryGetDestroyedAt(args.Target, out var max))
            return;

        var floor = max.Value.Float() * (1f - ent.Comp.RepairCap);
        var heal = MathF.Min(ent.Comp.Heal, damageable.TotalDamage.Float() - floor);
        if (heal > 0f)
            _damageable.HealEvenly((args.Target, damageable), -FixedPoint2.New(heal), origin: args.Shooter);
    }
}
