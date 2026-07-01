using System.Numerics;
using System.Text;
using Content.Server._FinalStand.NPC;
using Content.Server._FinalStand.Spawners;
using Content.Server.Administration;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.NPC;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Physics.Components;

namespace Content.Server._FinalStand.Commands;

// Dumps a snapshot of every wave zombie's state — position, target, speed, steering status,
// flow-field reachability, HTN keys. Useful for diagnosing idling / stranded / stuck zombies.
[AdminCommand(AdminFlags.Debug)]
public sealed class FSDebugZombiesCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly HordeFlowFieldSystem _flow = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override string Command => "fsdebugzombies";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var sb = new StringBuilder();
        var count = 0;
        var noPathCount = 0;
        var unreachableCount = 0;
        var stationaryCount = 0;

        sb.AppendLine($"[FSDebug] Flow field: HasField={_flow.HasField}");
        sb.AppendLine("uid | proto | tile | speed | status | reachable | target | htn-target | breach");

        var query = _ent.EntityQueryEnumerator<WaveSpawnedTagComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            count++;
            var worldPos = _transform.GetWorldPosition(xform);
            var gridUid = xform.GridUid;
            var tile = gridUid is { } g
                ? new Vector2i((int)MathF.Floor(xform.LocalPosition.X), (int)MathF.Floor(xform.LocalPosition.Y))
                : Vector2i.Zero;
            var reachable = gridUid is { } gg && _flow.IsReachable(gg, tile);
            if (!reachable) unreachableCount++;

            var speed = 0f;
            if (_ent.TryGetComponent<PhysicsComponent>(uid, out var phys))
                speed = phys.LinearVelocity.Length();
            if (speed < 0.1f) stationaryCount++;

            var status = "?";
            var targetCoords = "-";
            if (_ent.TryGetComponent<NPCSteeringComponent>(uid, out var steering))
            {
                status = steering.Status.ToString();
                if (steering.Status == SteeringStatus.NoPath) noPathCount++;
                targetCoords = steering.Coordinates.ToString();
            }

            var htnTarget = "-";
            var breach = "-";
            if (_ent.TryGetComponent<HTNComponent>(uid, out var htn))
            {
                if (htn.Blackboard.TryGetValue<EntityUid>("Target", out var t, _ent) && _ent.EntityExists(t))
                    htnTarget = _ent.ToPrettyString(t).ToString();
                if (htn.Blackboard.TryGetValue<EntityUid>(FSAIBlackboardKeys.BreachTarget, out var b, _ent) && _ent.EntityExists(b))
                    breach = _ent.ToPrettyString(b).ToString();
            }

            var proto = _ent.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID ?? "?";
            sb.AppendLine($"{uid} | {proto} | {tile} | {speed:F2} | {status} | {reachable} | {targetCoords} | {htnTarget} | {breach}");
        }

        sb.AppendLine($"--- Total: {count}, NoPath: {noPathCount}, Unreachable(flow): {unreachableCount}, Stationary(speed<0.1): {stationaryCount}");
        shell.WriteLine(sb.ToString());
    }
}
