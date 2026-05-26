using System.Numerics;
using Content.Server.Popups;
using Content.Shared._FinalStand.Akimbo;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server._FinalStand.Akimbo;

public sealed class FSAkimboSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    private static readonly Angle SpreadMinPenalty = Angle.FromDegrees(8);
    private static readonly Angle SpreadMaxPenalty = Angle.FromDegrees(12);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSAkimboGunComponent, GunShotEvent>(OnAkimboGunShot);
        SubscribeLocalEvent<FSAkimboGunComponent, GunRefreshModifiersEvent>(OnAkimboModifiers);
        SubscribeLocalEvent<FSAkimboGunComponent, ComponentShutdown>(OnAkimboShutdown);
        SubscribeLocalEvent<FSAkimboGunComponent, DroppedEvent>(OnAkimboDropped);
        // Fix 2: refresh stored hand names when the gun is picked up into a hand
        SubscribeLocalEvent<FSAkimboGunComponent, GotEquippedHandEvent>(OnAkimboEquippedInHand);
        // Fix 3: block pickup of a gun whose partner is still held by a different player
        SubscribeLocalEvent<FSAkimboGunComponent, GettingPickedUpAttemptEvent>(OnAkimboPickupAttempt);
        // Fix 4: throw both guns when either is thrown
        SubscribeLocalEvent<HandsComponent, BeforeThrowEvent>(OnBeforeThrow);
    }

    /// <summary>
    ///     After each shot, switch the active hand to the paired gun so the next
    ///     trigger pull fires it. This gives natural alternating fire for both
    ///     semi-auto (click per shot) and full-auto (held trigger).
    /// </summary>
    private void OnAkimboGunShot(EntityUid uid, FSAkimboGunComponent comp, ref GunShotEvent args)
    {
        if (comp.PairedGun == null || !comp.PairedGun.Value.IsValid())
            return;

        // Fix 2: dynamic lookup instead of stored name — stored PairedHand can be stale after owner change.
        if (!_hands.IsHolding(args.User, comp.PairedGun.Value, out var pairedHand) || pairedHand == null)
            return; // partner not in this user's hands — don't switch

        _hands.TrySetActiveHand(args.User, pairedHand);
    }

    private void OnAkimboModifiers(EntityUid uid, FSAkimboGunComponent comp, ref GunRefreshModifiersEvent args)
    {
        args.MinAngle += SpreadMinPenalty;
        args.MaxAngle += SpreadMaxPenalty;
    }

    private void OnAkimboShutdown(EntityUid uid, FSAkimboGunComponent comp, ComponentShutdown args)
    {
        if (comp.PairedGun == null)
            return;

        var paired = comp.PairedGun.Value;
        comp.PairedGun = null; // clear to prevent re-entry
        comp.MyHand = null;
        comp.PairedHand = null;

        if (!paired.IsValid())
            return;
        if (!TryComp<FSAkimboGunComponent>(paired, out var pairedComp))
            return;

        pairedComp.PairedGun = null; // clear partner's ref before removal
        RemComp<FSAkimboGunComponent>(paired);
    }

    private void OnAkimboDropped(EntityUid uid, FSAkimboGunComponent comp, DroppedEvent args)
    {
        // Dropping either gun breaks the akimbo pair entirely.
        // OnAkimboShutdown will cascade and remove from the partner.
        RemComp<FSAkimboGunComponent>(uid);
    }

    // Fix 2: update stored hand names when the gun enters a new player's hand
    private void OnAkimboEquippedInHand(EntityUid uid, FSAkimboGunComponent comp, GotEquippedHandEvent args)
    {
        // Resolve the actual hand-name string via IsHolding (Hand struct does not store its own key)
        if (!_hands.IsHolding(args.User, uid, out var myHand) || myHand == null)
            return;

        comp.MyHand = myHand;
        Dirty(uid, comp);

        if (comp.PairedGun == null || !comp.PairedGun.Value.IsValid())
            return;
        if (!TryComp<FSAkimboGunComponent>(comp.PairedGun.Value, out var pairedComp))
            return;

        pairedComp.PairedHand = myHand;
        Dirty(comp.PairedGun.Value, pairedComp);
    }

    // Fix 3: prevent picking up a gun whose partner is currently held by a different player
    private void OnAkimboPickupAttempt(EntityUid uid, FSAkimboGunComponent comp, GettingPickedUpAttemptEvent args)
    {
        if (comp.PairedGun == null || !comp.PairedGun.Value.IsValid())
            return;

        // When a gun is in a player's hands its transform parent is that player entity.
        var pairedParent = Transform(comp.PairedGun.Value).ParentUid;
        if (!pairedParent.IsValid() || pairedParent == args.User || !HasComp<HandsComponent>(pairedParent))
            return;

        args.Cancel();
        _popup.PopupEntity("This weapon is part of another player's akimbo pair.", uid, args.User);
    }

    // Fix 4: when either akimbo gun is thrown, simultaneously throw the partner with a 5° offset
    private void OnBeforeThrow(EntityUid uid, HandsComponent hands, ref BeforeThrowEvent ev)
    {
        if (!TryComp<FSAkimboGunComponent>(ev.ItemUid, out var akimbo)
            || akimbo.PairedGun == null
            || !akimbo.PairedGun.Value.IsValid())
            return;

        var partner = akimbo.PairedGun.Value;

        // Partner must still be in the same player's hands (pair dissolves on drop, so this should always be true)
        if (!_hands.IsHolding(uid, partner, out _))
            return;

        // Dissolve pair BEFORE dropping so OnAkimboDropped/Shutdown sees null refs and exits early.
        akimbo.PairedGun = null;
        if (TryComp<FSAkimboGunComponent>(partner, out var partnerComp))
            partnerComp.PairedGun = null;
        RemComp<FSAkimboGunComponent>(ev.ItemUid);
        RemComp<FSAkimboGunComponent>(partner);

        // Compute partner throw direction with a 5° offset so guns don't land on top of each other.
        var length = ev.Direction.Length();
        var partnerDir = (new Angle(ev.Direction) + Angle.FromDegrees(5.0)).ToVec() * length;

        // Drop the partner from hand and throw it.
        if (_hands.TryDrop(uid, partner))
            _throwing.TryThrow(partner, partnerDir, ev.ThrowSpeed, uid, recoil: false);
    }
}
