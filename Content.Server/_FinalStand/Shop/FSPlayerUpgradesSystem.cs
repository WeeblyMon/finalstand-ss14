using Content.Shared._FinalStand.Utility;
using Content.Server._FinalStand.Grenades;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Upgrades;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
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
    [Dependency] private FSChargeShotSystem _chargeShot = default!;
    [Dependency] private FSSpeedBonusSystem _speedBonus = default!;
    [Dependency] private FSGrenadeSelectActionSystem _grenadeSelect = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private FSItemStashSystem _stash = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, EntInsertedIntoContainerMessage>(OnMagInsertedToGun);
        SubscribeLocalEvent<GunComponent, GotEquippedHandEvent>(OnGunEquippedHand);
    }

    // Returns the weapon that ends up holding the upgrade, which differs when the upgrade replaces it.
    public EntityUid ApplySingleUpgrade(EntityUid weapon, EntityUid player, WeaponUpgradeDef def, int newLevel, bool spawnItems = true)
    {
        var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);

        if (!TryApplyGunStats(weapon, player, def, newLevel, spawnItems, state)
            && !TryApplyShotEffects(weapon, player, def, newLevel, spawnItems, state)
            && !TryApplyMelee(ref weapon, player, def, newLevel, spawnItems, state)
            && !TryApplyGrenades(weapon, player, def, newLevel, spawnItems, state)
            && !TryApplyEnergy(weapon, player, def, newLevel, spawnItems, state)
            && !TryApplyShield(weapon, player, def, newLevel, spawnItems, state))
        {
            TryApplyPayload(weapon, player, def, newLevel, spawnItems, state);
        }

        if (!TerminatingOrDeleted(weapon) && TryComp<FSWeaponUpgradeStateComponent>(weapon, out var finalState))
            Dirty(weapon, finalState);

        return weapon;
    }

    private EntityUid TryTransformToDualWieldEnergySword(EntityUid oldSword, EntityUid player,
        FSWeaponUpgradeStateComponent oldState, float attackSpeedBonus)
    {
        var coords = Transform(player).Coordinates;
        var newSword = Spawn("FSEnergySwordDouble", coords);

        var newState = EnsureComp<FSWeaponUpgradeStateComponent>(newSword);
        CopyUpgradeState(oldState, newState);
        newState.AttackSpeedMultiplier = oldState.AttackSpeedMultiplier + attackSpeedBonus;
        newState.DualWieldEnergySwordApplied = true;
        Dirty(newSword, newState);

        // The state is already on the new sword, so the old one always goes.
        var wasInHand = TryComp<HandsComponent>(player, out var hands)
            && _hands.IsHolding((player, hands), oldSword, out _)
            && _hands.TryDrop((player, hands), oldSword);
        QueueDel(oldSword);

        if (!wasInHand || !_hands.TryPickup(player, newSword, handsComp: hands))
            _stash.Stash(player, newSword);

        return newSword;
    }

    public void CopyUpgradeState(FSWeaponUpgradeStateComponent from, FSWeaponUpgradeStateComponent to)
    {
        _serialization.CopyTo(from, ref to, notNullableOverride: true);
    }

    public void ApplyMagSizeBonusToCurrentMag(EntityUid gun, int bonus)
    {
        if (!FSItemSlots.TryGetSlot(EntityManager, _itemSlots, gun, SharedGunSystem.MagazineSlot, out var slot))
            return;
        var mag = slot.Item;
        if (mag == null || !TryComp<BallisticAmmoProviderComponent>(mag.Value, out var bal))
            return;
        ResizeMagazine(bal, bonus);
        var upgraded = EnsureComp<FSMagUpgradedComponent>(mag.Value);
        upgraded.AppliedBonus += bonus;
        Dirty(mag.Value, bal);
    }

    private void OnMagInsertedToGun(EntityUid gun, FSWeaponUpgradeStateComponent state,
        EntInsertedIntoContainerMessage args)
    {
        if (!TryComp<BallisticAmmoProviderComponent>(args.Entity, out var bal)) return;

        var upgraded = EnsureComp<FSMagUpgradedComponent>(args.Entity);
        var diff = state.MagazineSizeBonus - upgraded.AppliedBonus;
        if (diff != 0)
        {
            ResizeMagazine(bal, diff);
            upgraded.AppliedBonus = state.MagazineSizeBonus;
            Dirty(args.Entity, bal);
        }

        var holder = Transform(gun).ParentUid;
        ReconcileMagazinePercent(gun, HasComp<HandsComponent>(holder) ? holder : null);
    }
}
