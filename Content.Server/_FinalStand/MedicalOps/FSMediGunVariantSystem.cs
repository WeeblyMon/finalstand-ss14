using Content.Server._FinalStand.Research;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.MedicalOps;

// Research replaces every medigun in the round with its variant, including ones spawned afterwards.
public sealed partial class FSMediGunVariantSystem : EntitySystem
{
    [Dependency] private FSMedicalResearchSystem _research = default!;
    [Dependency] private FSMediGunSystem _mediGun = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private static readonly EntProtoId OverHealerProto = "FSMedicalBeamGunOverHealer";
    private static readonly EntProtoId UberChargerProto = "FSMedicalBeamGunUberCharger";

    private readonly HashSet<EntityUid> _pending = new();
    private readonly List<EntityUid> _scratch = new();

    public FSMediGunVariant CurrentVariant()
    {
        if (_research.IsNodeUnlocked(FSMedicalUpgradeSystem.RefinedEmitters))
            return FSMediGunVariant.OverHealer;

        return _research.IsNodeUnlocked(FSMedicalUpgradeSystem.UberCycling)
            ? FSMediGunVariant.UberCharger
            : FSMediGunVariant.Base;
    }

    public void QueueIfOutdated(Entity<FSMediGunComponent> gun)
    {
        if (NeedsSwap(gun.Comp, CurrentVariant()))
            _pending.Add(gun);
    }

    public void SwapAll()
    {
        var variant = CurrentVariant();

        _scratch.Clear();
        var query = EntityQueryEnumerator<FSMediGunComponent>();
        while (query.MoveNext(out var uid, out var gun))
        {
            if (NeedsSwap(gun, variant))
                _scratch.Add(uid);
        }

        foreach (var uid in _scratch)
            Swap(uid, variant);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pending.Count == 0)
            return;

        var variant = CurrentVariant();
        _scratch.Clear();
        _scratch.AddRange(_pending);
        _pending.Clear();

        foreach (var uid in _scratch)
        {
            if (!TerminatingOrDeleted(uid) && TryComp<FSMediGunComponent>(uid, out var gun) && NeedsSwap(gun, variant))
                Swap(uid, variant);
        }
    }

    private static bool NeedsSwap(FSMediGunComponent gun, FSMediGunVariant variant) =>
        !gun.BeamFromSelf && variant != FSMediGunVariant.Base && gun.Variant == FSMediGunVariant.Base;

    private void Swap(EntityUid old, FSMediGunVariant variant)
    {
        var proto = variant == FSMediGunVariant.OverHealer ? OverHealerProto : UberChargerProto;

        _mediGun.DisableAllConnections((old, Comp<FSMediGunComponent>(old)));
        var charge = _battery.GetCharge(old);

        BaseContainer? container = null;
        string? hand = null;
        string? slot = null;

        if (_containers.TryGetContainingContainer((old, null, null), out container))
        {
            if (_hands.IsHolding(container.Owner, old, out var heldIn))
                hand = heldIn;
            else if (_inventory.TryGetContainingSlot((old, null, null), out var slotDef))
                slot = slotDef.Name;
        }

        var fresh = Spawn(proto, _xform.GetMapCoordinates(old));
        Del(old);

        _battery.SetCharge(fresh, charge);

        if (container == null)
            return;

        var holder = container.Owner;
        if (hand != null)
            _hands.TryPickup(holder, fresh, hand, checkActionBlocker: false, animate: false);
        else if (slot != null)
            _inventory.TryEquip(holder, fresh, slot, silent: true, force: true);
        else if (HasComp<StorageComponent>(holder))
            _storage.Insert(holder, fresh, out _, playSound: false);
        else
            _containers.Insert(fresh, container);
    }
}
