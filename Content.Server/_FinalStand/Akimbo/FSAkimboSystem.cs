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

    // set in OnBeforeThrow so OnAkimboDropped doesn't cascade a Q-drop during a throw
    private bool _throwingPaired;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSAkimboGunComponent, GunShotEvent>(OnAkimboGunShot);
        SubscribeLocalEvent<FSAkimboGunComponent, OnEmptyGunShotEvent>(OnAkimboEmpty);
        SubscribeLocalEvent<FSAkimboGunComponent, GunRefreshModifiersEvent>(OnAkimboModifiers);
        SubscribeLocalEvent<FSAkimboGunComponent, ComponentShutdown>(OnAkimboShutdown);
        SubscribeLocalEvent<FSAkimboGunComponent, DroppedEvent>(OnAkimboDropped);
        SubscribeLocalEvent<FSAkimboGunComponent, GotEquippedHandEvent>(OnAkimboEquippedInHand);
        SubscribeLocalEvent<FSAkimboGunComponent, GettingPickedUpAttemptEvent>(OnAkimboPickupAttempt);
        SubscribeLocalEvent<HandsComponent, BeforeThrowEvent>(OnBeforeThrow);
    }

    private void OnAkimboGunShot(EntityUid uid, FSAkimboGunComponent comp, ref GunShotEvent args)
    {
        if (comp.PairedGun == null || !comp.PairedGun.Value.IsValid())
            return;
        if (!_hands.IsHolding(args.User, comp.PairedGun.Value, out var pairedHand) || pairedHand == null)
            return;
        _hands.TrySetActiveHand(args.User, pairedHand);
    }

    private void OnAkimboEmpty(EntityUid uid, FSAkimboGunComponent comp, ref OnEmptyGunShotEvent args)
    {
        if (comp.PairedGun == null || !comp.PairedGun.Value.IsValid())
            return;
        if (!_hands.IsHolding(args.User, comp.PairedGun.Value, out var pairedHand) || pairedHand == null)
            return;
        _hands.TrySetActiveHand(args.User, pairedHand);
    }

    private void OnAkimboModifiers(EntityUid uid, FSAkimboGunComponent comp, ref GunRefreshModifiersEvent args)
    {
        if (comp.PairedHand == null)
            return;
        args.MinAngle += SpreadMinPenalty;
        args.MaxAngle += SpreadMaxPenalty;
    }

    private void OnAkimboShutdown(EntityUid uid, FSAkimboGunComponent comp, ComponentShutdown args)
    {
        if (comp.PairedGun == null)
            return;
        var paired = comp.PairedGun.Value;
        comp.PairedGun = null;
        if (!paired.IsValid() || !TryComp<FSAkimboGunComponent>(paired, out var pairedComp))
            return;
        pairedComp.PairedGun = null;
        RemComp<FSAkimboGunComponent>(paired);
    }

    private void OnAkimboDropped(EntityUid uid, FSAkimboGunComponent comp, DroppedEvent args)
    {
        comp.MyHand = null;
        comp.PairedHand = null;
        Dirty(uid, comp);

        if (comp.PairedGun != null && TryComp<FSAkimboGunComponent>(comp.PairedGun.Value, out var pairedComp))
        {
            pairedComp.PairedHand = null;
            Dirty(comp.PairedGun.Value, pairedComp);

            if (!_throwingPaired && args.User.IsValid() && _hands.IsHolding(args.User, comp.PairedGun.Value, out _))
                _hands.TryDrop(args.User, comp.PairedGun.Value);
        }
    }

    private void OnAkimboEquippedInHand(EntityUid uid, FSAkimboGunComponent comp, GotEquippedHandEvent args)
    {
        if (!_hands.IsHolding(args.User, uid, out var myHand) || myHand == null)
            return;

        comp.MyHand = myHand;
        Dirty(uid, comp);

        if (comp.PairedGun == null || !comp.PairedGun.Value.IsValid())
            return;
        var paired = comp.PairedGun.Value;
        if (!TryComp<FSAkimboGunComponent>(paired, out var pairedComp))
            return;

        var pairedParent = Transform(paired).ParentUid;

        if (!HasComp<HandsComponent>(pairedParent))
        {
            if (_hands.TryPickupAnyHand(args.User, paired)
                && _hands.IsHolding(args.User, paired, out var pairedHand)
                && pairedHand != null)
            {
                comp.PairedHand = pairedHand;
                pairedComp.MyHand = pairedHand;
                pairedComp.PairedHand = myHand;
                Dirty(paired, pairedComp);
            }
        }
        else if (pairedParent == args.User)
        {
            if (_hands.IsHolding(args.User, paired, out var pairedHand) && pairedHand != null)
            {
                comp.PairedHand = pairedHand;
                pairedComp.MyHand = pairedHand;
                pairedComp.PairedHand = myHand;
                Dirty(paired, pairedComp);
            }
        }
    }

    private void OnAkimboPickupAttempt(EntityUid uid, FSAkimboGunComponent comp, GettingPickedUpAttemptEvent args)
    {
        if (comp.PairedGun == null || !comp.PairedGun.Value.IsValid())
            return;
        var pairedParent = Transform(comp.PairedGun.Value).ParentUid;
        if (!pairedParent.IsValid() || pairedParent == args.User || !HasComp<HandsComponent>(pairedParent))
            return;
        args.Cancel();
        _popup.PopupEntity("This weapon is part of another player's akimbo pair.", uid, args.User);
    }

    private void OnBeforeThrow(EntityUid uid, HandsComponent hands, ref BeforeThrowEvent ev)
    {
        if (!TryComp<FSAkimboGunComponent>(ev.ItemUid, out var akimbo)
            || akimbo.PairedGun == null
            || !akimbo.PairedGun.Value.IsValid())
            return;

        var partner = akimbo.PairedGun.Value;
        if (!_hands.IsHolding(uid, partner, out _))
            return;

        var length = ev.Direction.Length();
        var partnerDir = (new Angle(ev.Direction) + Angle.FromDegrees(5.0)).ToVec() * length;
        _throwingPaired = true;
        if (_hands.TryDrop(uid, partner))
            _throwing.TryThrow(partner, partnerDir, ev.ThrowSpeed, uid, recoil: false);
        _throwingPaired = false;
    }
}
