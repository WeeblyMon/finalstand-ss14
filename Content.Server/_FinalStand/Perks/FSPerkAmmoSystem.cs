using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Utility;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Perks;

// Shadow Rounds refunds a shot by chance; Undying keeps the gun topped up.
public sealed partial class FSPerkAmmoSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private FSUndyingSystem _undying = default!;

    public void OnShot(EntityUid gun, EntityUid holder, FSPerkLevelsComponent perks)
    {
        if (_undying.IsActive(holder))
        {
            Refund(gun, fill: true);
            return;
        }

        var level = perks.GetSlottedLevel("ShadowRounds");
        if (level > 0 && _random.Prob(FSPerkBonusConstants.ShadowRoundsChance[Math.Min(level, FSPerkDef.MaxLevel) - 1]))
            Refund(gun, fill: false);
    }

    private void Refund(EntityUid gun, bool fill)
    {
        if (TryComp<BatteryAmmoProviderComponent>(gun, out var battery) && TryComp<BatteryComponent>(gun, out var cell))
        {
            if (fill)
                _battery.SetCharge((gun, cell), cell.MaxCharge);
            else
                _battery.ChangeCharge((gun, cell), battery.FireCost);
            return;
        }

        var source = gun;
        if (!HasComp<BallisticAmmoProviderComponent>(gun)
            && FSItemSlots.TryGetSlot(EntityManager, _slots, gun, SharedGunSystem.MagazineSlot, out var slot)
            && slot.Item is { } magazine)
        {
            source = magazine;
        }

        if (!TryComp<BallisticAmmoProviderComponent>(source, out var ballistic) || ballistic.Proto == null)
            return;

        var space = ballistic.Capacity - ballistic.Entities.Count - ballistic.UnspawnedCount;
        if (space <= 0)
            return;

        _gun.SetBallisticUnspawned((source, ballistic), ballistic.UnspawnedCount + (fill ? space : 1));
    }
}
