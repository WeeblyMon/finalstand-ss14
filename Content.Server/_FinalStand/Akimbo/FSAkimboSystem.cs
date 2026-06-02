using System.Numerics;
using Content.Shared._FinalStand.Akimbo;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server._FinalStand.Akimbo;

public sealed class FSAkimboSystem : EntitySystem
{
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly Angle SpreadMinPenalty = Angle.FromDegrees(8);
    private static readonly Angle SpreadMaxPenalty = Angle.FromDegrees(12);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSAkimboGunComponent, GunShotEvent>(OnAkimboGunShot);
        SubscribeLocalEvent<FSAkimboGunComponent, GunRefreshModifiersEvent>(OnAkimboModifiers);
    }

    private void OnAkimboGunShot(EntityUid uid, FSAkimboGunComponent comp, ref GunShotEvent args)
    {
        // Prevent recursion: Shoot() we call below also raises GunShotEvent.
        if (comp.FiringSecondShot)
            return;

        if (!TryComp<GunComponent>(uid, out var gunComp))
            return;

        // ShootCoordinates holds the current aim position — still set after the first shot fires.
        if (gunComp.ShootCoordinates == null)
            return;

        // Try to consume one more round from the same magazine.
        var ammoList = new List<(EntityUid? Entity, IShootable Shootable)>();
        var takeAmmoEv = new TakeAmmoEvent(1, ammoList, Transform(uid).Coordinates, args.User);
        RaiseLocalEvent(uid, takeAmmoEv);

        if (takeAmmoEv.Ammo.Count == 0)
            return; // Only 1 round was left — graceful degradation, first projectile already fired.

        // Compute a ±0.3 tile perpendicular offset so the second projectile originates
        // from a distinct muzzle position, making the dual-fire feel like two guns.
        var gunCoords = Transform(uid).Coordinates;
        var gunMapPos = _transform.ToMapCoordinates(gunCoords).Position;
        var aimMapPos = _transform.ToMapCoordinates(gunComp.ShootCoordinates.Value).Position;
        var delta = aimMapPos - gunMapPos;
        var fromCoords = gunCoords;
        if (delta.LengthSquared() > 0.001f)
        {
            var firingDir = delta.Normalized();
            var perp = new Vector2(-firingDir.Y, firingDir.X);
            fromCoords = gunCoords.Offset(perp * comp.MuzzleOffset);
        }

        // Fire second projectile through the full Shoot() pipeline so cartridge handling,
        // sounds, and muzzle flash all work correctly.
        comp.FiringSecondShot = true;
        _gun.Shoot(
            (uid, gunComp),
            takeAmmoEv.Ammo,
            fromCoords,
            gunComp.ShootCoordinates.Value,
            out _,
            args.User);
        comp.FiringSecondShot = false;

        // Sync the ammo counter immediately so the client shows both decrements on the same tick.
        _gun.UpdateAmmoCount(uid);
    }

    private void OnAkimboModifiers(EntityUid uid, FSAkimboGunComponent comp, ref GunRefreshModifiersEvent args)
    {
        args.MinAngle += SpreadMinPenalty;
        args.MaxAngle += SpreadMaxPenalty;
    }
}
