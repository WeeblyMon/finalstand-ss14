using Content.Shared._FinalStand.Shop;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Upgrades.Effects;

// proc chance per shot to refund the energy cost; battery must be directly on the gun entity
public sealed partial class MagEfficiencyUpgradeSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedBatterySystem _battery = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public void HandleAmmoShot(EntityUid uid, FSWeaponUpgradeStateComponent state, AmmoShotEvent args)
    {
        if (state.MagEfficiencyLevel <= 0)
            return;
        if (!TryComp<BatteryAmmoProviderComponent>(uid, out var bat))
            return;

        if (!_random.Prob(state.MagEfficiencyLevel * 0.1f))
            return;

        _battery.ChangeCharge(uid, bat.FireCost);
    }
}
