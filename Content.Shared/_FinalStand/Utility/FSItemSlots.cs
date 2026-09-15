// TryGetSlot without the error log for entities that have no item slots at all.
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._FinalStand.Utility;

public static class FSItemSlots
{
    /// <summary>
    /// ItemSlotsSystem.TryGetSlot resolves ItemSlotsComponent and logs an error when it is missing,
    /// so asking a revolver for its magazine slot floods the log every frame. This answers false.
    /// </summary>
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
