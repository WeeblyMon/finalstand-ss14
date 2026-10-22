using System.Numerics;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Upgrades;
using Content.Shared._FinalStand.Weapons.Prediction;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanBasicRaycastSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ISharedAdminLogManager _log = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedFSLagCompensationSystem _lagCompensation = default!; // FINALSTAND

    [Dependency] private EntityQuery<HitscanBasicVisualsComponent> _visualsQuery = default!;

    private EntityQuery<FSFriendlyFireComponent> _ffQuery;
    private EntityQuery<FSHitscanPierceSkipComponent> _skipQuery;

    public override void Initialize()
    {
        base.Initialize();
        _ffQuery = GetEntityQuery<FSFriendlyFireComponent>(); // FINALSTAND
        _skipQuery = GetEntityQuery<FSHitscanPierceSkipComponent>();

        SubscribeLocalEvent<HitscanBasicRaycastComponent, HitscanTraceEvent>(OnHitscanFired);
    }

    private void OnHitscanFired(Entity<HitscanBasicRaycastComponent> ent, ref HitscanTraceEvent args)
    {
        var preTrace = new HitscanPreTraceEvent
        {
            FromCoordinates = args.FromCoordinates,
            ShotDirection = args.ShotDirection,
            Gun = args.Gun,
            Shooter = args.Shooter,
            Target = args.Target,
        };
        RaiseLocalEvent(ent, ref preTrace);

        var shooter = args.Shooter ?? args.Gun;
        var mapCords = _transform.ToMapCoordinates(args.FromCoordinates);
        var ray = new CollisionRay(mapCords.Position, args.ShotDirection, (int) ent.Comp.CollisionMask);
        var rayCastResults = _lagCompensation.IntersectRay(mapCords.MapId, ray, ent.Comp.MaxDistance, shooter, args.Shooter); // FINALSTAND

        var target = args.Target;
        var isFriendlyShooter = args.Shooter != null && _ffQuery.HasComponent(args.Shooter.Value);
        _skipQuery.TryGetComponent(ent, out var skipComp);

        // If you are in a container, use the raycast result
        // Otherwise:
        //  1.) Hit the first entity that you targeted.
        //  2.) Hit the first entity that doesn't require you to aim at it specifically to be hit.
        //  3.) Skip friendly players (pass-through with no collision/VFX).
        //  4.) Skip entities marked for pierce pre-pass by FSHitscanPierceFriendlySystem.
        EntityUid? vehicle = _container.TryGetContainingContainer(shooter, out var holder) ? holder.Owner : null;
        var result = _container.IsEntityOrParentInContainer(shooter)
            ? rayCastResults.FirstOrNull(hit => hit.HitEntity != vehicle)
            : rayCastResults.FirstOrNull(hit =>
                (hit.HitEntity == target || CompOrNull<RequireProjectileTargetComponent>(hit.HitEntity)?.Active != true)
                && !(isFriendlyShooter && _ffQuery.HasComponent(hit.HitEntity))
                && !(skipComp != null && skipComp.Targets.Contains(hit.HitEntity)));

        var distanceTried = result?.Distance ?? ent.Comp.MaxDistance;

        // Do visuals without an event. They should always happen and putting it on the attempt event is weird!
        // If more stuff gets added here, it should probably be turned into an event.
        var predictedBy = _lagCompensation.IsPredictedShot(args.Shooter) && _lagCompensation.CanPredictHitscan(ent) // FINALSTAND
            ? args.Shooter
            : null;
        FireEffects(args.FromCoordinates, distanceTried, args.ShotDirection.ToAngle(), ent.Owner, args.Shooter, predictedBy);

        // Admin logging
        if (result?.HitEntity != null)
        {
            _log.Add(LogType.HitScanHit,
                $"{ToPrettyString(shooter):user} hit {ToPrettyString(result.Value.HitEntity):target}"
                + $" using {ToPrettyString(args.Gun):entity}.");
        }

        var data = new HitscanRaycastFiredData
        {
            ShotDirection = args.ShotDirection,
            Gun = args.Gun,
            Shooter = args.Shooter,
            HitEntity = result?.HitEntity,
        };

        var attemptEvent = new AttemptHitscanRaycastFiredEvent { Data = data };
        RaiseLocalEvent(ent, ref attemptEvent);

        if (attemptEvent.Cancelled)
            return;

        var hitEvent = new HitscanRaycastFiredEvent { Data = data };
        RaiseLocalEvent(ent, ref hitEvent);
    }

    /// <summary>
    /// Create visual effects for the fired hitscan weapon.
    /// </summary>
    /// <param name="fromCoordinates">Location to start the effect.</param>
    /// <param name="distance">Distance of the hitscan shot.</param>
    /// <param name="shotAngle">Angle of the shot.</param>
    /// <param name="hitscanUid">The hitscan entity itself.</param>
    private void FireEffects(EntityCoordinates fromCoordinates, float distance, Angle shotAngle, EntityUid hitscanUid, EntityUid? shooter, EntityUid? predictedBy) // FINALSTAND
    {
        if (BuildEffects(fromCoordinates, distance, shotAngle, hitscanUid, shooter) is not { Origin: { } origin } effects) // FINALSTAND
            return;

        var filter = Filter.Pvs(GetCoordinates(origin), entityMan: EntityManager); // FINALSTAND
        if (predictedBy != null)
            filter.RemoveWhereAttachedEntity(e => e == predictedBy.Value);

        RaiseNetworkEvent(effects, filter);
    }

    public SharedGunSystem.HitscanEvent? BuildEffects(EntityCoordinates fromCoordinates, float distance, Angle shotAngle, EntityUid hitscanUid, EntityUid? shooter) // FINALSTAND
    {
        if (distance == 0 || !_visualsQuery.TryComp(hitscanUid, out var vizComp))
            return null;

        var sprites = new List<(NetCoordinates coordinates, Angle angle, SpriteSpecifier sprite, float scale, SharedGunSystem.HitscanPart part)>();
        var fromXform = Transform(fromCoordinates.EntityId);

        // We'll get the effects relative to the grid / map of the firer
        // Look you could probably optimise this a bit with redundant transforms at this point.

        var gridUid = fromXform.GridUid;
        if (gridUid != fromCoordinates.EntityId && TryComp(gridUid, out TransformComponent? gridXform))
        {
            var (_, gridRot, gridInvMatrix) = _transform.GetWorldPositionRotationInvMatrix(gridXform);
            var map = _transform.ToMapCoordinates(fromCoordinates);
            fromCoordinates = new EntityCoordinates(gridUid.Value, Vector2.Transform(map.Position, gridInvMatrix));
            shotAngle -= gridRot;
        }
        else
        {
            shotAngle -= _transform.GetWorldRotation(fromXform);
        }

        if (distance >= 1f)
        {
            if (vizComp.MuzzleFlash != null)
            {
                var coords = fromCoordinates.Offset(shotAngle.ToVec().Normalized() / 2);
                var netCoords = GetNetCoordinates(coords);

                sprites.Add((netCoords, shotAngle, vizComp.MuzzleFlash, 1f, SharedGunSystem.HitscanPart.Muzzle));
            }

            if (vizComp.TravelFlash != null)
            {
                var coords = fromCoordinates.Offset(shotAngle.ToVec() * (distance + 0.5f) / 2);
                var netCoords = GetNetCoordinates(coords);

                sprites.Add((netCoords, shotAngle, vizComp.TravelFlash, distance - 1.5f, SharedGunSystem.HitscanPart.Travel));
            }
        }

        if (vizComp.ImpactFlash != null)
        {
            var coords = fromCoordinates.Offset(shotAngle.ToVec() * distance);
            var netCoords = GetNetCoordinates(coords);

            sprites.Add((netCoords, shotAngle.FlipPositive(), vizComp.ImpactFlash, 1f, SharedGunSystem.HitscanPart.Impact));
        }

        if (sprites.Count == 0) // FINALSTAND
            return null;

        return new SharedGunSystem.HitscanEvent
        {
            Sprites = sprites,
            Shooter = shooter is { } user ? GetNetEntity(user) : null,
            Origin = GetNetCoordinates(fromCoordinates),
            Impact = GetNetCoordinates(fromCoordinates.Offset(shotAngle.ToVec() * distance)),
        };
    }
}
