using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Movement.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Deployables;

// Reuses FSSlowedComponent for the slow effect - refreshing rather than stacking keeps overlapping fields from producing a stronger/permanent slow.
public sealed partial class FSNullFieldSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan RefreshDuration = TimeSpan.FromSeconds(1);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSNullFieldComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var field, out var xform))
        {
            var worldPos = _transform.GetWorldPosition(uid);
            var candidates = new HashSet<Entity<WaveSpawnedTagComponent>>();
            _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(
                new MapCoordinates(worldPos, xform.MapID),
                field.Radius,
                candidates);

            foreach (var (targetUid, _) in candidates)
            {
                var slow = EnsureComp<FSSlowedComponent>(targetUid);
                slow.SlowFactor = field.SlowFactor;
                slow.EndTime = _timing.CurTime + RefreshDuration;
                _movement.RefreshMovementSpeedModifiers(targetUid);
            }
        }
    }
}
