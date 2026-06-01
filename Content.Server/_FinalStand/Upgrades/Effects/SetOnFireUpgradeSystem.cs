using Content.Server.Atmos.EntitySystems;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Atmos.Components;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed class SetOnFireUpgradeSystem : EntitySystem
{
    [Dependency] private readonly FlammableSystem _flammable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(ev.Weapon.Value, out var state) || !state.SetOnFireEnabled)
            return;
        if (HasComp<FSFriendlyFireComponent>(ev.Target))
            return;
        if (!TryComp<FlammableComponent>(ev.Target, out var flammable))
            return;

        // TODO(finalstand): tune fire stacks / duration
        flammable.FireStacks += 3f;
        _flammable.Ignite(ev.Target, ev.Shooter ?? ev.Target, flammable, ignitionSourceUser: ev.Shooter);
    }
}
