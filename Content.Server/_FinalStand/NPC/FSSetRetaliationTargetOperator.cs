using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._FinalStand.NPC;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.NPC;


public sealed partial class FSSetRetaliationTargetOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(FSAIBlackboardKeys.LastAttacker, out var target, _entManager)
            || !_entManager.EntityExists(target))
            return (false, null);

        return (true, new Dictionary<string, object>
        {
            { "Target", target },
            { "TargetCoordinates", new EntityCoordinates(target, Vector2.Zero) },
        });
    }
}
