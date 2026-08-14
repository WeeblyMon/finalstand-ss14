using System.Numerics;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared._FinalStand.Weapons;

// Hitscan raycast that skips non-mob entities (walls, windows, tables) and friendly players,
// letting X-Ray shots punch through solid matter to reach the first zombie in line.
public sealed partial class FSXrayRaycastSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private FSHitscanCoordSystem _hitscanCoords = default!;

    private EntityQuery<FSFriendlyFireComponent> _ffQuery;
    private EntityQuery<MobStateComponent> _mobQuery;
    private EntityQuery<HitscanBasicVisualsComponent> _visualsQuery;
    private EntityQuery<FSWeaponUpgradeStateComponent> _upgradeQuery;

    public override void Initialize()
    {
        base.Initialize();
        _ffQuery = GetEntityQuery<FSFriendlyFireComponent>();
        _mobQuery = GetEntityQuery<MobStateComponent>();
        _visualsQuery = GetEntityQuery<HitscanBasicVisualsComponent>();
        _upgradeQuery = GetEntityQuery<FSWeaponUpgradeStateComponent>();
        SubscribeLocalEvent<FSXrayRaycastComponent, HitscanTraceEvent>(OnTrace);
    }

    private void OnTrace(Entity<FSXrayRaycastComponent> ent, ref HitscanTraceEvent args)
    {
        var shooter = args.Shooter ?? args.Gun;
        var mapCoords = _transform.ToMapCoordinates(args.FromCoordinates);
        var ray = new CollisionRay(mapCoords.Position, args.ShotDirection, (int) CollisionGroup.Opaque);
        var results = _physics.IntersectRay(mapCoords.MapId, ray, ent.Comp.MaxDistance, shooter, false);

        var isFriendly = args.Shooter != null && _ffQuery.HasComponent(args.Shooter.Value);

        var pierceCount = 0;
        if (_upgradeQuery.TryGetComponent(args.Gun, out var upgradeState))
            pierceCount = (int) Math.Round(upgradeState.PierceThreshold.Float());

        // Iterate all ray hits; skip non-mobs (walls, tables) and friendly players.
        // With pierce upgrades, damage intermediate targets and continue to the next one.
        RayCastResults? hit = null;
        var totalDist = ent.Comp.MaxDistance;
        foreach (var r in results)
        {
            if (!_mobQuery.HasComponent(r.HitEntity)) continue;
            if (isFriendly && _ffQuery.HasComponent(r.HitEntity)) continue;

            if (pierceCount > 0)
            {
                var pierceHitEvent = new HitscanRaycastFiredEvent
                {
                    Data = new HitscanRaycastFiredData
                    {
                        ShotDirection = args.ShotDirection,
                        Gun = args.Gun,
                        Shooter = args.Shooter,
                        HitEntity = r.HitEntity,
                    }
                };
                RaiseLocalEvent(ent, ref pierceHitEvent);
                totalDist = r.Distance;
                pierceCount--;
                continue;
            }

            hit = r;
            totalDist = r.Distance;
            break;
        }

        FireEffects(args.FromCoordinates, totalDist, args.ShotDirection.ToAngle(), ent.Owner);

        var data = new HitscanRaycastFiredData
        {
            ShotDirection = args.ShotDirection,
            Gun = args.Gun,
            Shooter = args.Shooter,
            HitEntity = hit?.HitEntity,
        };

        var attempt = new AttemptHitscanRaycastFiredEvent { Data = data };
        RaiseLocalEvent(ent, ref attempt);
        if (attempt.Cancelled) return;

        var hitEvent = new HitscanRaycastFiredEvent { Data = data };
        RaiseLocalEvent(ent, ref hitEvent);
    }

    private void FireEffects(EntityCoordinates fromCoordinates, float distance, Angle shotAngle, EntityUid hitscanUid)
    {
        if (distance == 0 || !_visualsQuery.TryGetComponent(hitscanUid, out var vizComp))
            return;

        var sprites = new List<(NetCoordinates coordinates, Angle angle, SpriteSpecifier sprite, float scale)>();
        (fromCoordinates, shotAngle) = _hitscanCoords.ToGridRelative(fromCoordinates, shotAngle);

        if (distance >= 1f)
        {
            if (vizComp.MuzzleFlash != null)
            {
                var coords = fromCoordinates.Offset(shotAngle.ToVec().Normalized() / 2);
                sprites.Add((GetNetCoordinates(coords), shotAngle, vizComp.MuzzleFlash, 1f));
            }

            if (vizComp.TravelFlash != null)
            {
                var coords = fromCoordinates.Offset(shotAngle.ToVec() * (distance + 0.5f) / 2);
                sprites.Add((GetNetCoordinates(coords), shotAngle, vizComp.TravelFlash, distance - 1.5f));
            }
        }

        if (vizComp.ImpactFlash != null)
        {
            var coords = fromCoordinates.Offset(shotAngle.ToVec() * distance);
            sprites.Add((GetNetCoordinates(coords), shotAngle.FlipPositive(), vizComp.ImpactFlash, 1f));
        }

        if (sprites.Count > 0)
        {
            RaiseNetworkEvent(new SharedGunSystem.HitscanEvent { Sprites = sprites },
                Filter.Pvs(fromCoordinates, entityMan: EntityManager));
        }
    }
}
