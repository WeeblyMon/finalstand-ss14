using Content.Server._FinalStand.Perks;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades;
using Content.Shared.Damage.Components;
using Content.Shared.Mind;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Hitscan.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Server._FinalStand.Upgrades;

// Pre-pass: damages pierce targets and adds them to skip list before HitscanBasicRaycastSystem runs.
// HitscanBasicRaycastSystem's filter then skips those targets and draws one beam to the final hit.
// Friendly pass-through is handled entirely inside HitscanBasicRaycastSystem's filter.
public sealed partial class FSHitscanPierceFriendlySystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    private EntityQuery<FSFriendlyFireComponent> _ffQuery;
    private EntityQuery<WaveSpawnedTagComponent> _waveQuery;
    private EntityQuery<RequireProjectileTargetComponent> _reqTargetQuery;

    public override void Initialize()
    {
        base.Initialize();
        _ffQuery = GetEntityQuery<FSFriendlyFireComponent>();
        _waveQuery = GetEntityQuery<WaveSpawnedTagComponent>();
        _reqTargetQuery = GetEntityQuery<RequireProjectileTargetComponent>();

        SubscribeLocalEvent<HitscanBasicRaycastComponent, HitscanPreTraceEvent>(OnHitscanTrace);
    }

    private void OnHitscanTrace(EntityUid uid, HitscanBasicRaycastComponent raycastComp, ref HitscanPreTraceEvent args)
    {
        var shopPierce = TryComp<FSWeaponUpgradeStateComponent>(args.Gun, out var upgradeState)
            ? (int) Math.Round(upgradeState.PierceThreshold.Float())
            : 0;

        var perkPierce = 0;
        if (args.Shooter != null
            && _mind.TryGetMind(args.Shooter.Value, out var mindId, out _)
            && TryComp<FSPerkLevelsComponent>(mindId, out var perks))
        {
            perkPierce = perks.GetSlottedLevel("DeepImpact");
        }

        var pierceCount = Math.Max(shopPierce, perkPierce);
        if (pierceCount <= 0)
            return;

        var shooter = args.Shooter ?? args.Gun;
        if (_container.IsEntityOrParentInContainer(shooter))
            return;

        // Persistent per-shot dedup: skipComp lives on the ammo entity for the whole shot, so it
        // survives even if this pre-pass ends up running more than once (e.g. a duplicate trace
        // raise). A local per-call HashSet would reset each time and re-fire the hit event.
        var skipComp = EnsureComp<FSHitscanPierceSkipComponent>(uid);
        pierceCount -= skipComp.Targets.Count;
        if (pierceCount <= 0)
            return;

        var mapCoords = _transform.ToMapCoordinates(args.FromCoordinates);
        var ray = new CollisionRay(mapCoords.Position, args.ShotDirection, (int) raycastComp.CollisionMask);
        var results = _physics.IntersectRay(mapCoords.MapId, ray, raycastComp.MaxDistance, shooter, false);

        var isFriendlyShooter = args.Shooter != null && _ffQuery.HasComponent(args.Shooter.Value);

        foreach (var hit in results)
        {
            var hitEnt = hit.HitEntity;

            if (_reqTargetQuery.TryGetComponent(hitEnt, out var reqTarget) && reqTarget.Active && hitEnt != args.Target)
                continue;

            if (isFriendlyShooter && _ffQuery.HasComponent(hitEnt))
                continue;

            if (_waveQuery.HasComponent(hitEnt) && pierceCount > 0)
            {
                if (!skipComp.Targets.Add(hitEnt))
                    continue;

                var hitEvent = new HitscanRaycastFiredEvent
                {
                    Data = new HitscanRaycastFiredData
                    {
                        ShotDirection = args.ShotDirection,
                        Gun = args.Gun,
                        Shooter = args.Shooter,
                        HitEntity = hitEnt,
                    }
                };
                RaiseLocalEvent(uid, ref hitEvent);

                pierceCount--;
                continue;
            }

            break;
        }
    }
}
