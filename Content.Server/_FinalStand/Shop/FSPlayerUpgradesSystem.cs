using Content.Server._FinalStand.Grenades;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Upgrades;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Serialization.Manager;

namespace Content.Server._FinalStand.Shop;

public sealed partial class FSPlayerUpgradesSystem : EntitySystem
{
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private FSGrenadeSelectActionSystem _grenadeSelect = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private FSItemStashSystem _stash = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, EntInsertedIntoContainerMessage>(OnMagInsertedToGun);
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, EntRemovedFromContainerMessage>(OnMagRemovedFromGun);
    }

    public void ApplySingleUpgrade(EntityUid weapon, EntityUid player, WeaponUpgradeDef def, int newLevel, bool spawnItems = true)
    {
        // Resolved once here since nearly every upgrade writes to it; each partial-file group returns
        // false for a type it doesn't handle, so the first group that claims the type wins.
        var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);

        if (TryApplyGunStats(weapon, player, def, newLevel, spawnItems, state)) return;
        if (TryApplyShotEffects(weapon, player, def, newLevel, spawnItems, state)) return;
        if (TryApplyMelee(weapon, player, def, newLevel, spawnItems, state)) return;
        if (TryApplyGrenades(weapon, player, def, newLevel, spawnItems, state)) return;
        if (TryApplyShield(weapon, player, def, newLevel, spawnItems, state)) return;
        TryApplyPayload(weapon, player, def, newLevel, spawnItems, state);
    }

    private void TryTransformToDualWieldEnergySword(EntityUid oldSword, EntityUid player,
        FSWeaponUpgradeStateComponent oldState, float attackSpeedBonus)
    {
        var coords = Transform(player).Coordinates;
        var newSword = Spawn("FSEnergySwordDouble", coords);

        var newState = EnsureComp<FSWeaponUpgradeStateComponent>(newSword);
        CopyUpgradeState(oldState, newState);
        newState.AttackSpeedMultiplier = oldState.AttackSpeedMultiplier + attackSpeedBonus;
        newState.DualWieldEnergySwordApplied = true;

        // only delete old sword if we can safely drop it — avoids silently losing upgrade state
        var safeToDelete = true;
        var wasInHand = false;
        if (TryComp<HandsComponent>(player, out var hands)
            && _hands.IsHolding((player, hands), oldSword, out _))
        {
            wasInHand = true;
            safeToDelete = _hands.TryDrop((player, hands), oldSword);
            if (!safeToDelete)
                Log.Warning($"DualWieldEnergySword: TryDrop failed for {ToPrettyString(oldSword)} on {ToPrettyString(player)} — keeping old sword to avoid losing upgrade state.");
        }

        if (safeToDelete)
            QueueDel(oldSword);

        if (wasInHand && hands != null && _hands.TryPickup(player, newSword, handsComp: hands))
            return;

        _stash.Stash(player, newSword);
    }

    // Copies every DataField on the component — a hand-written field list would drop any upgrade added later.
    private void CopyUpgradeState(FSWeaponUpgradeStateComponent from, FSWeaponUpgradeStateComponent to)
    {
        _serialization.CopyTo(from, ref to, notNullableOverride: true);
    }

    private void ApplyMagSizeBonusToCurrentMag(EntityUid gun, int bonus)
    {
        if (!_itemSlots.TryGetSlot(gun, SharedGunSystem.MagazineSlot, out var slot))
            return;
        var mag = slot.Item;
        if (mag == null || !TryComp<BallisticAmmoProviderComponent>(mag.Value, out var bal))
            return;
#pragma warning disable RA0002
        bal.Capacity += bonus;
        bal.UnspawnedCount = Math.Min(bal.UnspawnedCount + bonus, bal.Capacity);
#pragma warning restore RA0002
        var upgraded = EnsureComp<FSMagUpgradedComponent>(mag.Value);
        upgraded.AppliedBonus += bonus;
        Dirty(mag.Value, bal);
    }

    private void OnMagInsertedToGun(EntityUid gun, FSWeaponUpgradeStateComponent state,
        EntInsertedIntoContainerMessage args)
    {
        if (state.MagazineSizeBonus <= 0) return;
        if (!TryComp<BallisticAmmoProviderComponent>(args.Entity, out var bal)) return;

        var upgraded = EnsureComp<FSMagUpgradedComponent>(args.Entity);
        var diff = state.MagazineSizeBonus - upgraded.AppliedBonus;
        if (diff <= 0) return;

#pragma warning disable RA0002
        bal.Capacity += diff;
        bal.UnspawnedCount = Math.Min(bal.UnspawnedCount + diff, bal.Capacity);
#pragma warning restore RA0002
        upgraded.AppliedBonus = state.MagazineSizeBonus;
        Dirty(args.Entity, bal);
    }

    private void OnMagRemovedFromGun(EntityUid gun, FSWeaponUpgradeStateComponent state,
        EntRemovedFromContainerMessage args)
    {
        if (!TryComp<FSMagUpgradedComponent>(args.Entity, out var upgraded) || upgraded.AppliedBonus <= 0)
            return;
        if (!TryComp<BallisticAmmoProviderComponent>(args.Entity, out var bal))
            return;

#pragma warning disable RA0002
        bal.Capacity -= upgraded.AppliedBonus;
        bal.UnspawnedCount = Math.Min(bal.UnspawnedCount, bal.Capacity);
#pragma warning restore RA0002
        upgraded.AppliedBonus = 0;
        Dirty(args.Entity, bal);
    }
}
