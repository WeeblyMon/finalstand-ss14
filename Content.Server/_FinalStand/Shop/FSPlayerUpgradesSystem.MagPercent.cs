using Content.Server._FinalStand.Perks;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Utility;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server._FinalStand.Shop;

// Percentage magazine bonuses (Ordnance research and Bandolier), sized from the magazine's base capacity.
// Bandolier follows whoever last equipped the gun or loaded it, so holstering never changes capacity.
public sealed partial class FSPlayerUpgradesSystem
{
    private void OnGunEquippedHand(EntityUid gun, GunComponent _, GotEquippedHandEvent args)
    {
        ReconcileMagazinePercent(gun, args.User);
    }

    public void ReconcileHeldMagazines(EntityUid body)
    {
        if (!TryComp<HandsComponent>(body, out var hands))
            return;

        foreach (var held in _hands.EnumerateHeld((body, hands)))
        {
            if (HasComp<GunComponent>(held))
                ReconcileMagazinePercent(held, body);
        }
    }

    public void ReconcileMagazinePercent(EntityUid gun, EntityUid? holder)
    {
        var target = gun;
        if (!HasComp<BallisticAmmoProviderComponent>(gun))
        {
            if (!FSItemSlots.TryGetSlot(EntityManager, _itemSlots, gun, SharedGunSystem.MagazineSlot, out var slot)
                || slot.Item is not { } mag)
                return;
            target = mag;
        }

        if (!TryComp<BallisticAmmoProviderComponent>(target, out var bal))
            return;

        var percent = CompOrNull<FSWeaponUpgradeStateComponent>(gun)?.ResearchMagazinePercent ?? 0f;
        percent += BandolierPercent(holder);

        var upgraded = EnsureComp<FSMagUpgradedComponent>(target);
        var baseCapacity = bal.Capacity - upgraded.AppliedBonus - upgraded.AppliedPercentBonus;
        var wanted = percent >= 0f
            ? (int) MathF.Ceiling(baseCapacity * percent)
            : -(int) MathF.Round(baseCapacity * -percent);
        wanted = Math.Max(wanted, 1 - baseCapacity);

        var diff = wanted - upgraded.AppliedPercentBonus;
        if (diff == 0)
            return;

        ResizeMagazine(bal, upgraded, diff);
        upgraded.AppliedPercentBonus = wanted;
        Dirty(target, bal);
        _gun.UpdateBallisticAppearance((target, bal));
    }

    // Resizing keeps the rounds a magazine has: a shrink remembers what it dropped and the next grow hands it back.
    // The one free fill is a full magazine growing past the largest it has ever been (a new upgrade, a fresh mag in
    // an upgraded gun). Refilling whenever a full one grew let a mag swapped between guns with different bonuses
    // top itself up every swap.
    private static void ResizeMagazine(BallisticAmmoProviderComponent bal, FSMagUpgradedComponent upgraded, int diff)
    {
        var count = bal.Entities.Count + bal.UnspawnedCount;
        var wasFull = count >= bal.Capacity;
        if (upgraded.PeakCapacity < bal.Capacity)
            upgraded.PeakCapacity = bal.Capacity;

#pragma warning disable RA0002
        var oldCapacity = bal.Capacity;
        bal.Capacity += diff;
        if (diff > 0 && bal.Proto != null)
        {
            var refund = Math.Min(diff, upgraded.ClippedRounds);
            var fill = wasFull ? Math.Max(0, bal.Capacity - Math.Max(oldCapacity, upgraded.PeakCapacity)) : 0;
            upgraded.ClippedRounds -= refund;
            bal.UnspawnedCount += refund + fill;
        }

        bal.UnspawnedCount = Math.Clamp(bal.UnspawnedCount, 0, Math.Max(0, bal.Capacity - bal.Entities.Count));
#pragma warning restore RA0002

        if (diff < 0)
            upgraded.ClippedRounds += count - (bal.Entities.Count + bal.UnspawnedCount);
        upgraded.PeakCapacity = Math.Max(upgraded.PeakCapacity, bal.Capacity);
    }

    private float BandolierPercent(EntityUid? holder)
    {
        if (holder is not { } body
            || !_mind.TryGetMind(body, out var mindId, out _)
            || !TryComp<FSPerkLevelsComponent>(mindId, out var perks))
            return 0f;

        return perks.GetSlottedLevel("Bandolier") * FSPerkBonusConstants.BandolierPerLevel;
    }
}
