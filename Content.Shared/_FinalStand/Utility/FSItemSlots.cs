// TryGetSlot without the error log for entities that have no item slots at all.
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._FinalStand.Utility;

public static class FSItemSlots
{
    public static bool TryGetSlot(
        IEntityManager entMan,
        ItemSlotsSystem slots,
        EntityUid uid,
        string slotId,
        [NotNullWhen(true)] out ItemSlot? slot)
    {
        slot = null;
        return entMan.TryGetComponent<ItemSlotsComponent>(uid, out var comp)
               && slots.TryGetSlot(uid, slotId, out slot, comp);
    }
}
