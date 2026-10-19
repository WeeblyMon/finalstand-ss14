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

        ResizeMagazine(bal, diff);
        upgraded.AppliedPercentBonus = wanted;
        Dirty(target, bal);
        _gun.UpdateBallisticAppearance((target, bal));
    }

    // A full magazine stays full when it grows; nothing else gains rounds (an empty magazine used to fill up
    // just by going into an upgraded gun), and shrinking drops whatever no longer fits.
    private static void ResizeMagazine(BallisticAmmoProviderComponent bal, int diff)
    {
        var wasFull = bal.Entities.Count + bal.UnspawnedCount >= bal.Capacity;
#pragma warning disable RA0002
        bal.Capacity += diff;
        if (diff > 0 && wasFull && bal.Proto != null)
            bal.UnspawnedCount += diff;
        bal.UnspawnedCount = Math.Clamp(bal.UnspawnedCount, 0, Math.Max(0, bal.Capacity - bal.Entities.Count));
#pragma warning restore RA0002
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
