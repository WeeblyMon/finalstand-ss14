// One traversal of everything a player carries, for the shop's buy/upgrade/sell paths.
using Content.Shared._FinalStand.Shop;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.Containers;

namespace Content.Server._FinalStand.Shop;

public enum CarryKind : byte
{
    Hand,
    Equipped,
    Backpack,
}

// Where is a hand name for CarryKind.Hand, a slot id for CarryKind.Equipped.
public readonly record struct CarriedItem(EntityUid Uid, CarryKind Kind, string Where);

public sealed partial class FSInventorySearchSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;

    // Every carried item matching protoIds, in hand then slot then backpack order.
    public void Collect(EntityUid player, IReadOnlySet<string> protoIds, List<CarriedItem> results,
        bool requireUpgradeState = false)
    {
        Walk(player, protoIds, requireUpgradeState, results, out _);
    }

    // The first carried item matching protoIds. Stops at the first hit.
    public CarriedItem? FindFirst(EntityUid player, IReadOnlySet<string> protoIds, bool requireUpgradeState = false)
    {
        Walk(player, protoIds, requireUpgradeState, null, out var first);
        return first;
    }

    private static IEnumerable<string> ActiveHandFirst(HandsComponent hands)
    {
        if (hands.ActiveHandId is { } active && hands.Hands.ContainsKey(active))
            yield return active;

        foreach (var handName in hands.SortedHands)
        {
            if (handName != hands.ActiveHandId)
                yield return handName;
        }
    }

    // Passing a null sink means "stop at the first match" — the two public entry points differ only in that.
    private void Walk(EntityUid player, IReadOnlySet<string> protoIds, bool requireUpgradeState,
        List<CarriedItem>? sink, out CarriedItem? first)
    {
        first = null;

        bool Accept(EntityUid ent, CarryKind kind, string where, ref CarriedItem? found)
        {
            var proto = MetaData(ent).EntityPrototype?.ID;
            if (proto == null || !protoIds.Contains(proto))
                return false;
            if (requireUpgradeState && !HasComp<FSWeaponUpgradeStateComponent>(ent))
                return false;

            var item = new CarriedItem(ent, kind, where);
            found ??= item;
            sink?.Add(item);
            return sink == null;
        }

        if (TryComp<HandsComponent>(player, out var hands))
        {
            foreach (var handName in ActiveHandFirst(hands))
            {
                if (!_hands.TryGetHeldItem((player, hands), handName, out var held) || held == null)
                    continue;
                if (Accept(held.Value, CarryKind.Hand, handName, ref first))
                    return;
            }
        }

        foreach (var slot in FSItemStashSystem.SlotPriority)
        {
            if (!_inventory.TryGetSlotEntity(player, slot, out var slotEnt) || slotEnt == null)
                continue;
            if (Accept(slotEnt.Value, CarryKind.Equipped, slot, ref first))
                return;
        }

        if (!_inventory.TryGetSlotEntity(player, "back", out var backpack) || backpack == null)
            return;
        if (!TryComp<ContainerManagerComponent>(backpack.Value, out var cm))
            return;

        foreach (var container in cm.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                if (Accept(item, CarryKind.Backpack, "back", ref first))
                    return;
            }
        }
    }
}
