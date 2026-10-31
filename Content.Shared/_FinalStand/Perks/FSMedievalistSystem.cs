using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Armor;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Perks;

public sealed partial class FSMedievalistSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly string[] ArmourSlots = ["outerClothing", "head"];
    private const SlotFlags ArmourSlotFlags = SlotFlags.OUTERCLOTHING | SlotFlags.HEAD;

    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Slash = "Slash";
    private static readonly ProtoId<DamageTypePrototype> Piercing = "Piercing";

    // Average brute coefficient below which a worn piece counts as armour.
    private const float BruteArmourCoefficient = 0.95f;
    private static readonly TimeSpan PopupCooldown = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMedievalistComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<FSMedievalistComponent, IsEquippingAttemptEvent>(OnEquipping);
        SubscribeLocalEvent<FSMedievalistComponent, IsEquippingTargetAttemptEvent>(OnEquippingTarget);
    }

    public bool IsForbiddenArmour(EntityUid item, SlotFlags slot)
        => (slot & ArmourSlotFlags) != 0 && IsBruteArmour(item);

    public bool IsBruteArmour(EntityUid item)
    {
        if (!TryComp<ArmorComponent>(item, out var armor))
            return false;

        var coefficients = armor.Modifiers.Coefficients;
        var average = (coefficients.GetValueOrDefault(Blunt.Id, 1f)
                       + coefficients.GetValueOrDefault(Slash.Id, 1f)
                       + coefficients.GetValueOrDefault(Piercing.Id, 1f)) / 3f;
        return average < BruteArmourCoefficient;
    }

    public void StripArmour(EntityUid body)
    {
        foreach (var slot in ArmourSlots)
        {
            if (_inventory.TryGetSlotEntity(body, slot, out var worn) && IsBruteArmour(worn.Value))
                _inventory.TryUnequip(body, slot, silent: true, force: true);
        }
    }

    private void OnShotAttempted(Entity<FSMedievalistComponent> ent, ref ShotAttemptedEvent args)
    {
        if (HasComp<FSRivetGunComponent>(args.Used) || HasComp<FSMediGunComponent>(args.Used))
            return;

        args.Cancel();
        Popup(ent, "fs-medievalist-no-guns");
    }

    private void OnEquipping(EntityUid uid, FSMedievalistComponent comp, IsEquippingAttemptEvent args)
    {
        if (args.EquipTarget == uid && IsForbiddenArmour(args.Equipment, args.SlotFlags))
            Refuse(args);
    }

    private void OnEquippingTarget(EntityUid uid, FSMedievalistComponent comp, IsEquippingTargetAttemptEvent args)
    {
        if (IsForbiddenArmour(args.Equipment, args.SlotFlags))
            Refuse(args);
    }

    private static void Refuse(EquipAttemptBase args)
    {
        args.Reason = "fs-medievalist-no-armour";
        args.Cancel();
    }

    private void Popup(Entity<FSMedievalistComponent> ent, LocId message)
    {
        if (_timing.CurTime < ent.Comp.NextPopup)
            return;

        ent.Comp.NextPopup = _timing.CurTime + PopupCooldown;
        _popup.PopupClient(Loc.GetString(message), ent, ent);
    }
}
