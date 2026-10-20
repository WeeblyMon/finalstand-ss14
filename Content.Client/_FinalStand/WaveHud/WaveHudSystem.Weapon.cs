using Robust.Shared.Input;
using System.Diagnostics.CodeAnalysis;
using Content.Shared._FinalStand.Grenades;
using Content.Client._FinalStand.Engineering;
using Content.Client._FinalStand.RCD;
using Content.Shared._FinalStand.HMG;
using Content.Shared.Mech.Components;
using Content.Shared.Vehicle.Components;
using Content.Shared._FinalStand.Materials;
using Content.Shared._FinalStand.RCD;
using Content.Shared.RCD.Components;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Repair;
using Content.Shared._FinalStand.Utility;
using Content.Shared.Charges.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Robust.Client.Input;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Tools.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudSystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityWhitelistSystem _magWhitelist = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private FSSyringeGunExamineSystem _syringeGun = default!;
    [Dependency] private FSCarriedMaterialSystem _materials = default!;
    [Dependency] private FSHMGSystem _hmg = default!;
    [Dependency] private FSToolCardSystem _toolCard = default!;

    private FSHudText<int> _magsReserveText = default!;
    private FSHudText<(string Mode, string Key)> _toolModeText = default!;
    private FSHudText<int> _chargeText = default!;
    private FSHudText<(FixedPoint2 Volume, FixedPoint2 Max)> _volumeText = default!;
    private FSHudText<int> _stackText = default!;
    private FSHudText<(int Left, int Max)> _chargesText = default!;
    private FSHudText<int> _storageText = default!;

    private void InitializeWeaponText()
    {
        _magsReserveText = new(count => Loc.GetString("fs-hud-mags-reserve", ("count", count)));
        _toolModeText = new(v => Loc.GetString("fs-hud-tool-mode", ("mode", v.Mode.ToLowerInvariant()), ("key", v.Key)));
        _chargeText = new(percent => Loc.GetString("fs-hud-item-charge", ("percent", percent)));
        _volumeText = new(v => Loc.GetString("fs-hud-item-volume", ("volume", v.Volume), ("max", v.Max)));
        _stackText = new(count => Loc.GetString("fs-hud-item-stack", ("count", count)));
        _chargesText = new(v => Loc.GetString("fs-hud-item-charges", ("charges", v.Left), ("max", v.Max)));
        _storageText = new(count => Loc.GetString("fs-hud-item-storage", ("count", count)));
    }

    private void UpdateWeapon(WaveHudOverlay overlay)
    {
        overlay.WeaponName = null;
        overlay.WeaponLoaded = null;
        overlay.WeaponCapacity = null;
        overlay.WeaponReserve = 0;
        overlay.WeaponReserveText = null;
        overlay.HasAmmoChoice = false;
        overlay.HasThrowChoice = false;
        overlay.WeaponTakesMagazines = false;
        overlay.ItemDetail = null;
        overlay.Uber = null;
        overlay.ReloadKey = KeyText(ContentKeyFunctions.ReloadWeapon, "R");

        if (_player.LocalEntity is not { } player)
            return;

        overlay.HasThrowChoice = CountGrenadeTypes(player) > 1;

        if (TryComp<FSHMGGunnerComponent>(player, out var gunner) && TryComp<FSHMGComponent>(gunner.Hmg, out var hmg))
        {
            UpdateHmg(overlay, (gunner.Hmg, hmg));
            return;
        }

        if (TryComp<VehicleOperatorComponent>(player, out var pilot) && TryComp<MechComponent>(pilot.Vehicle, out var mech))
        {
            UpdateMech(overlay, (pilot.Vehicle.Value, mech));
            return;
        }

        if (!_hands.TryGetActiveItem(player, out var active))
            return;

        // A drag or two-handed wield parks a placeholder in the hand; only a drag is worth naming.
        if (TryComp<VirtualItemComponent>(active, out var virtualItem))
        {
            if (TryComp<PullerComponent>(player, out var puller) && puller.Pulling == virtualItem.BlockingEntity)
                overlay.WeaponName = Loc.GetString("fs-hud-dragging", ("target", Name(virtualItem.BlockingEntity)));

            return;
        }

        var held = active.Value;

        overlay.WeaponName = Name(held);

        if (!HasComp<GunComponent>(held))
        {
            overlay.ItemDetail = GetItemDetail(held);
            UpdateUberMeter(overlay, held);
            return;
        }

        var ammoEv = new GetAmmoCountEvent();
        RaiseLocalEvent(held, ref ammoEv);

        if (ammoEv.Capacity > 0)
        {
            overlay.WeaponLoaded = ammoEv.Count;
            overlay.WeaponCapacity = ammoEv.Capacity;
        }

        overlay.WeaponReserve = CountSpareMagazines(player, held);
        overlay.HasAmmoChoice = overlay.WeaponReserve > 0;
        overlay.WeaponTakesMagazines = TryGetMagazineSlot(held, out _);

        if (overlay.WeaponTakesMagazines)
            overlay.WeaponReserveText = _magsReserveText.Get(overlay.WeaponReserve);
        else if (TryComp<FSRivetGunComponent>(held, out var rivetGun) && !_toolCard.Covers(held))
            overlay.WeaponReserveText = Loc.GetString("fs-hud-rivet-steel", ("count", _materials.Count(player, rivetGun.Material)));

        if (TryComp<FSSyringeGunComponent>(held, out var syringeGun)
            && _syringeGun.TryGetLoad((held, syringeGun), out var mix, out _))
            overlay.ItemDetail = mix;
    }

    private void UpdateMech(WaveHudOverlay overlay, Entity<MechComponent> mech)
    {
        if (mech.Comp.CurrentSelectedEquipment is { } equipment)
        {
            overlay.WeaponName = Name(equipment);
            var ammo = new GetAmmoCountEvent();
            RaiseLocalEvent(equipment, ref ammo);
            if (ammo.Capacity > 0)
            {
                overlay.WeaponLoaded = ammo.Count;
                overlay.WeaponCapacity = ammo.Capacity;
            }
        }
        else
        {
            overlay.WeaponName = Loc.GetString("fs-hud-mech-no-weapon", ("mech", Name(mech)));
        }
    }

    private void UpdateHmg(WaveHudOverlay overlay, Entity<FSHMGComponent> hmg)
    {
        overlay.WeaponName = Name(hmg);
        var ammo = new GetAmmoCountEvent();
        RaiseLocalEvent(hmg, ref ammo);
        overlay.WeaponLoaded = ammo.Count;
        overlay.WeaponCapacity = ammo.Capacity;

        var heat = (int) MathF.Round(_hmg.CurrentHeat(hmg.Comp) * 100f);
        overlay.ItemDetail = _hmg.IsOverheated(hmg.Comp)
            ? Loc.GetString("fs-hud-hmg-overheated")
            : TryComp<FSAmmoCrateComponent>(hmg.Comp.Crate, out var crate)
                ? Loc.GetString("fs-hud-hmg-detail", ("crate", crate.Rounds), ("heat", heat))
                : Loc.GetString("fs-hud-hmg-no-crate", ("heat", heat));
    }

    // Hints name the player's own bindings; the fallback is the default when nothing is bound.
    private string KeyText(BoundKeyFunction function, string fallback)
    {
        return _input.TryGetKeyBinding(function, out var binding) ? binding.GetKeyString() : fallback;
    }

    private void UpdateUberMeter(WaveHudOverlay overlay, EntityUid held)
    {
        if (!TryComp<FSMediGunComponent>(held, out var gun) || gun.Variant != FSMediGunVariant.UberCharger)
            return;

        var left = gun.UberEndTime is { } end ? (float) Math.Max(0, (end - _timing.CurTime).TotalSeconds) : 0f;
        var key = KeyText(ContentKeyFunctions.FSUberCharge, "Alt+E");

        overlay.Uber = new WaveHudOverlay.UberMeter(gun.UberCharge, gun.UberActive, left, gun.BeamColor, key);
    }

    private string? GetItemDetail(EntityUid held)
    {
        // Use-in-hand falls through to activation, which is what cycles the mode.
        if (TryComp<MultipleToolComponent>(held, out var multi)
            && multi.CurrentEntry < multi.Entries.Length
            && !string.IsNullOrEmpty(multi.CurrentQualityName))
        {
            return _toolModeText.Get((multi.CurrentQualityName, KeyText(ContentKeyFunctions.UseItemInHand, "Z")));
        }

        if (TryComp<FSEngineerOnlyRCDComponent>(held, out var fsRcd) && TryComp<RCDComponent>(held, out var rcd))
            return _toolCard.Covers(held) ? null : FSRCDText.Summary(ProtoMan, _materials.CountAll(_player.LocalEntity), fsRcd, ProtoMan.Index(rcd.ProtoId));

        if (TryComp<BatteryComponent>(held, out var battery) && battery.MaxCharge > 0f)
            return _chargeText.Get((int) MathF.Round(_battery.GetChargeLevel((held, battery)) * 100f, MidpointRounding.AwayFromZero));

        if (_solutions.TryGetDrainableSolution(held, out _, out var drainable))
            return _volumeText.Get((drainable.Volume, drainable.MaxVolume));

        if (_solutions.TryGetRefillableSolution(held, out _, out var refillable))
            return _volumeText.Get((refillable.Volume, refillable.MaxVolume));

        if (TryComp<StackComponent>(held, out var stack))
            return _stackText.Get(stack.Count);

        if (TryComp<LimitedChargesComponent>(held, out var charges))
            return _chargesText.Get((charges.LastCharges, charges.MaxCharges));

        if (TryComp<StorageComponent>(held, out var storage))
            return _storageText.Get(storage.Container.ContainedEntities.Count);

        return null;
    }

    private int CountGrenadeTypes(EntityUid player)
    {
        var seen = new HashSet<GrenadeType>();
        Walk(player, 0);
        return seen.Count;

        void Walk(EntityUid root, int depth)
        {
            if (!TryComp<ContainerManagerComponent>(root, out var mgr))
                return;

            foreach (var container in mgr.Containers.Values)
            {
                foreach (var item in container.ContainedEntities)
                {
                    if (TryComp<FSGrenadePackComponent>(item, out var pack) && pack.Stock > 0)
                        seen.Add(pack.PackType);

                    if (depth < 1)
                        Walk(item, depth + 1);
                }
            }
        }
    }

    private bool TryGetMagazineSlot(EntityUid gun, [NotNullWhen(true)] out ItemSlot? slot) =>
        FSItemSlots.TryGetSlot(EntityManager, _itemSlots, gun, SharedGunSystem.MagazineSlot, out slot);

    private int CountSpareMagazines(EntityUid player, EntityUid gun)
    {
        if (!TryGetMagazineSlot(gun, out var magSlot))
            return 0;

        var count = 0;
        Walk(player, 0);
        return count;

        void Walk(EntityUid root, int depth)
        {
            if (!TryComp<ContainerManagerComponent>(root, out var mgr))
                return;

            foreach (var container in mgr.Containers.Values)
            {
                foreach (var item in container.ContainedEntities)
                {
                    if (item != magSlot.Item && !_magWhitelist.IsWhitelistFail(magSlot.Whitelist, item))
                        count++;

                    if (depth < 1)
                        Walk(item, depth + 1);
                }
            }
        }
    }
}
