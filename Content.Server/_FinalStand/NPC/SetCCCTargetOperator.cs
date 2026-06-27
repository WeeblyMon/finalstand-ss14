using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._FinalStand.NPC;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.NPC;

public sealed partial class SetCCCTargetOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private const float ApproachRadius = 1.0f;

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(NPCBlackboard.CurrentOrderedTarget, out var target, _entManager)
            || !_entManager.EntityExists(target))
            return (false, null);

        var angle = blackboard.TryGetValue<float>(FSAIBlackboardKeys.ApproachAngle, out var a, _entManager) ? a : 0f;
        var offset = new Vector2(MathF.Cos(angle) * ApproachRadius, MathF.Sin(angle) * ApproachRadius);

        return (true, new Dictionary<string, object>
        {
            { "Target", target },
            { "TargetCoordinates", new EntityCoordinates(target, offset) },
        });
    }
}
