using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared._FinalStand.Akimbo;

public sealed class FSAkimboSystem : EntitySystem
{
    [Dependency] private readonly SharedVirtualItemSystem _virtualItems = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private static readonly Angle SpreadMinPenalty = Angle.FromDegrees(8);
    private static readonly Angle SpreadMaxPenalty = Angle.FromDegrees(12);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSAkimboGunComponent, TakeAmmoEvent>(OnAkimboTakeAmmo);
        SubscribeLocalEvent<FSAkimboGunComponent, GunRefreshModifiersEvent>(OnAkimboModifiers);
        SubscribeLocalEvent<FSAkimboGunComponent, ComponentStartup>(OnAkimboStartup);
        SubscribeLocalEvent<FSAkimboGunComponent, GotEquippedHandEvent>(OnAkimboEquipped);
        SubscribeLocalEvent<FSAkimboGunComponent, DroppedEvent>(OnAkimboDropped);
        SubscribeLocalEvent<FSAkimboGunComponent, ComponentShutdown>(OnAkimboShutdown);
        SubscribeLocalEvent<FSAkimboGunComponent, VirtualItemDeletedEvent>(OnMirrorDeleted);
    }

    private void OnAkimboStartup(EntityUid uid, FSAkimboGunComponent comp, ComponentStartup args)
    {
        if (TryGetHolder(uid, out var holder))
            _virtualItems.TrySpawnVirtualItemInHand(uid, holder);
    }

    private void OnAkimboEquipped(EntityUid uid, FSAkimboGunComponent comp, GotEquippedHandEvent args)
    {
        _virtualItems.TrySpawnVirtualItemInHand(uid, args.User);
    }

    private void OnAkimboDropped(EntityUid uid, FSAkimboGunComponent comp, DroppedEvent args)
    {
        _virtualItems.DeleteInHandsMatching(args.User, uid);
    }

    private void OnAkimboShutdown(EntityUid uid, FSAkimboGunComponent comp, ComponentShutdown args)
    {
        if (TryGetHolder(uid, out var holder))
            _virtualItems.DeleteInHandsMatching(holder, uid);
    }

    // If the mirror is destroyed while the real gun is still held (player pressed Q on the mirror hand),
    // drop the real gun so both hands clear cleanly. Re-spawning would make the mirror undismissable.
    private void OnMirrorDeleted(EntityUid uid, FSAkimboGunComponent comp, VirtualItemDeletedEvent args)
    {
        if (!TryGetHolder(uid, out var holder))
            return;
        _hands.TryDrop(holder, uid);
    }

    private bool TryGetHolder(EntityUid uid, out EntityUid holder)
    {
        var parent = Transform(uid).ParentUid;
        if (HasComp<HandsComponent>(parent))
        {
            holder = parent;
            return true;
        }
        holder = default;
        return false;
    }

    private void OnAkimboTakeAmmo(EntityUid uid, FSAkimboGunComponent comp, TakeAmmoEvent args)
    {
        // Guard against recursion when we raise the inner TakeAmmoEvent below.
        if (comp.FiringSecondShot)
            return;

        // Nothing was taken by the ammo provider — nothing to double.
        if (args.Ammo.Count == 0)
            return;

        // Pull one more round from the same provider and append it to the ammo list.
        // Shoot() then fires both in one call, so casing ejection is fully handled
        // by the normal pipeline on both client and server — no prediction mismatch.
        comp.FiringSecondShot = true;
        var extraList = new List<(EntityUid? Entity, IShootable Shootable)>();
        RaiseLocalEvent(uid, new TakeAmmoEvent(1, extraList, args.Coordinates, args.User));
        comp.FiringSecondShot = false;

        args.Ammo.AddRange(extraList);
    }

    private void OnAkimboModifiers(EntityUid uid, FSAkimboGunComponent comp, ref GunRefreshModifiersEvent args)
    {
        args.MinAngle += SpreadMinPenalty;
        args.MaxAngle += SpreadMaxPenalty;
    }
}
