using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared.Administration.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Respawn;

public sealed class FSWaveRespawnSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
    }

    private void OnWaveEnded(ref WaveEndedEvent ev)
    {
        // Collect all respawn point coordinates.
        var points = new List<EntityCoordinates>();
        var pointQuery = EntityQueryEnumerator<FSRespawnPointComponent, TransformComponent>();
        while (pointQuery.MoveNext(out _, out _, out var xform))
            points.Add(xform.Coordinates);

        if (points.Count == 0)
            return;

        // Teleport and rejuvenate every downed player.
        var actorQuery = EntityQueryEnumerator<ActorComponent>();
        while (actorQuery.MoveNext(out var uid, out _))
        {
            if (!_mobState.IsIncapacitated(uid))
                continue;

            _transform.SetCoordinates(uid, _random.Pick(points));
            _rejuvenate.PerformRejuvenate(uid);
        }
    }
}
