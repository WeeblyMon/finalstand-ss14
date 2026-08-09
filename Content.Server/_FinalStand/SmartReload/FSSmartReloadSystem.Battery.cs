using Content.Shared.Containers.ItemSlots;
using Content.Shared.Power.Components;

namespace Content.Server._FinalStand.SmartReload;

public sealed partial class FSSmartReloadSystem : EntitySystem
{
    private void ReloadBattery(EntityUid gun, EntityUid user)
    {
        if (!HasComp<ItemSlotsComponent>(gun) || !_slots.TryGetSlot(gun, "gun_cell", out _))
        {
            var msg = HasComp<BatterySelfRechargerComponent>(gun)
                ? "This weapon self-recharges."
                : "Needs cell recharger.";
            _popup.PopupEntity(msg, gun, user);
            return;
        }

        var currentCell = _slots.TryGetSlot(gun, "gun_cell", out var cellSlot) ? cellSlot!.Item : null;

        // Find the replacement before ejecting, otherwise a player with no spare loses the cell
        // they were using and the gun ends up empty.
        var newCell = FindBestPowerCell(user, currentCell);
        if (newCell == null)
        {
            _popup.PopupEntity("No power cell found.", gun, user);
            return;
        }

        _slots.TryEject(gun, "gun_cell", user, out _);
        _slots.TryInsert(gun, "gun_cell", newCell.Value, user);
    }
}
