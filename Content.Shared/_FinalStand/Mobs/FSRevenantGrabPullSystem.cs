using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Mobs;

public sealed partial class FSRevenantGrabPullSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly List<EntityUid> _expired = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _expired.Clear();

        var query = EntityQueryEnumerator<FSRevenantGrabbedComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var grabbed, out var body))
        {
            if (now >= grabbed.EndsAt)
            {
                _expired.Add(uid);
                continue;
            }

            if (TerminatingOrDeleted(grabbed.Puller))
                continue;

            var myPos = _transform.GetMapCoordinates(uid);
            var pullerPos = _transform.GetMapCoordinates(grabbed.Puller);

            if (myPos.MapId == MapId.Nullspace || myPos.MapId != pullerPos.MapId)
                continue;

            var delta = pullerPos.Position - myPos.Position;
            var distance = delta.Length();

            if (distance <= grabbed.StopRange)
                continue;

            _physics.SetLinearVelocity(uid, delta / distance * grabbed.PullSpeed, body: body);
        }

        if (!_net.IsServer)
            return;

        foreach (var uid in _expired)
            RemComp<FSRevenantGrabbedComponent>(uid);
    }
}
