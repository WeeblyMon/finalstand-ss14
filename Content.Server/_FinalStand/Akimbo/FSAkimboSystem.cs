using Content.Shared._FinalStand.Akimbo;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server._FinalStand.Akimbo;

public sealed class FSAkimboSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private static readonly Angle SpreadMinPenalty = Angle.FromDegrees(8);
    private static readonly Angle SpreadMaxPenalty = Angle.FromDegrees(12);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSAkimboGunComponent, GunShotEvent>(OnAkimboGunShot);
        SubscribeLocalEvent<FSAkimboGunComponent, GunRefreshModifiersEvent>(OnAkimboModifiers);
        SubscribeLocalEvent<FSAkimboGunComponent, ComponentShutdown>(OnAkimboShutdown);
        SubscribeLocalEvent<FSAkimboGunComponent, DroppedEvent>(OnAkimboDropped);
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
        if (comp.PairedHand == null)
            return;

        _hands.TrySetActiveHand(args.User, comp.PairedHand);
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
}
