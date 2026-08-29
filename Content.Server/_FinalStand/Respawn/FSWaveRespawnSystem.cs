using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared.Administration.Systems;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Respawn;

public sealed partial class FSWaveRespawnSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private RejuvenateSystem _rejuvenate = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
    }

    private void OnWaveEnded(ref WaveEndedEvent ev)
    {
        var points = new List<EntityCoordinates>();
        var pointQuery = EntityQueryEnumerator<FSRespawnPointComponent, TransformComponent>();
        while (pointQuery.MoveNext(out _, out _, out var xform))
            points.Add(xform.Coordinates);

        if (points.Count == 0)
            return;

        var mindQuery = EntityQueryEnumerator<MindContainerComponent, MobStateComponent>();
        while (mindQuery.MoveNext(out var uid, out var mindContainer, out _))
        {
            if (!mindContainer.HasMind)
                continue;
            if (!_mobState.IsIncapacitated(uid))
                continue;

            // The pull joint survives a teleport and yanks the puller across the map with the body.
            if (TryComp<PullableComponent>(uid, out var pullable))
                _pulling.TryStopPull(uid, pullable);

            _transform.SetCoordinates(uid, _random.Pick(points));
            _rejuvenate.PerformRejuvenate(uid);
        }
    }
}
