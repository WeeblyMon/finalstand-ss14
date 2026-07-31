using System.Numerics;
using Content.Server._FinalStand.Mobs;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Mobs;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.Deployables;

// Reuses FSDamageVulnerabilitySystem for the damage buff - refreshing rather than stacking keeps overlapping beacons from multiplying damage twice.
public sealed class FSDamageBeaconSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly FSDamageVulnerabilitySystem _vulnerability = default!;

    private const float RefreshDuration = 1f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSDamageBeaconComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, FSDamageBeaconComponent comp, MapInitEvent args)
    {
        // Parented directly to the beacon so it auto-deletes with it.
        if (comp.FieldVfxProtoId is { } vfxProto)
            Spawn(vfxProto, new EntityCoordinates(uid, Vector2.Zero));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSDamageBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var beacon, out var xform))
        {
            var worldPos = _transform.GetWorldPosition(uid);
            var candidates = new HashSet<Entity<WaveSpawnedTagComponent>>();
            _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(
                new MapCoordinates(worldPos, xform.MapID),
                beacon.Radius,
                candidates);

            foreach (var (targetUid, _) in candidates)
            {
                _vulnerability.Apply(targetUid, RefreshDuration);
                var vuln = EnsureComp<FSDamageVulnerabilityComponent>(targetUid);
                vuln.DamageMultiplier = beacon.DamageMultiplier;
            }
        }
    }
}
