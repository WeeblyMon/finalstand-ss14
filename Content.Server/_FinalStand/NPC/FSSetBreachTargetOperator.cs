using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._FinalStand.NPC;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.NPC;

public sealed partial class FSSetBreachTargetOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private const float GoldenStride = 2.399963f;
    private const float ApproachRadius = 0.85f;

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(FSAIBlackboardKeys.BreachTarget, out var target, _entManager)
            || !_entManager.EntityExists(target))
            return (false, null);

        var self = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var angle = ((int)self * GoldenStride) % MathF.Tau;
        var offset = new Vector2(MathF.Cos(angle) * ApproachRadius, MathF.Sin(angle) * ApproachRadius);

        return (true, new Dictionary<string, object>
        {
            { "Target", target },
            { "TargetCoordinates", new EntityCoordinates(target, offset) },
        });
    }
}
