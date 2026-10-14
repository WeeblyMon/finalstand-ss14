using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Utility;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
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
    [Dependency] private SharedHandsSystem _hands = default!;

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

    public void FillHeld(EntityUid holder)
    {
        foreach (var held in _hands.EnumerateHeld(holder))
            Refund(held, fill: true);
    }

    private void Refund(EntityUid gun, bool fill)
    {
        if (TryComp<BatteryAmmoProviderComponent>(gun, out var battery) && TryComp<BatteryComponent>(gun, out var cell))
        {
            if (!fill)
                _battery.ChangeCharge((gun, cell), battery.FireCost);
            else if (!_battery.IsFull((gun, cell)))
                _battery.SetCharge((gun, cell), cell.MaxCharge);
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
        if (space > 0)
            _gun.SetBallisticUnspawned((source, ballistic), ballistic.UnspawnedCount + (fill ? space : 1));

        if (fill)
            Chamber(gun);
    }

    private void Chamber(EntityUid gun)
    {
        if (!TryComp<ChamberMagazineAmmoProviderComponent>(gun, out var chamber)
            || chamber.BoltClosed == null
            || _gun.GetChamberEntity(gun) != null)
            return;

        if (chamber.BoltClosed == true)
            _gun.SetBoltClosed(gun, chamber, false);
        _gun.SetBoltClosed(gun, chamber, true);
    }
}
