// Applies a movement-speed bonus to the wielder ONLY while a weapon with HeldSpeedBonusPercent is held.
using Content.Shared._FinalStand.Shop;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Movement.Systems;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed partial class HeldSpeedBonusSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, HeldRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnHeldRefresh);
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, GotUnequippedHandEvent>(OnUnequipped);
    }

    private void OnHeldRefresh(EntityUid weapon, FSWeaponUpgradeStateComponent state, HeldRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        if (state.HeldSpeedBonusPercent <= 0f)
            return;
        var mult = 1f + state.HeldSpeedBonusPercent;
        args.Args.ModifySpeed(mult, mult);
    }

    private void OnEquipped(EntityUid weapon, FSWeaponUpgradeStateComponent state, GotEquippedHandEvent args)
    {
        if (state.HeldSpeedBonusPercent > 0f)
            _movement.RefreshMovementSpeedModifiers(args.User);
    }

    private void OnUnequipped(EntityUid weapon, FSWeaponUpgradeStateComponent state, GotUnequippedHandEvent args)
    {
        if (state.HeldSpeedBonusPercent > 0f)
            _movement.RefreshMovementSpeedModifiers(args.User);
    }
}
