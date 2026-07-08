using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.NPC;

// Overrides TargetCoordinates with a per-zombie angular offset around the melee target so
// wave zombies spread into a ring instead of all converging on the same pixel.
public sealed partial class FSSetMeleeApproachOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    // Golden-angle stride so each UID maps to a distinct point on the ring.
    private const float GoldenStride = 2.399963f; // 2π × golden-ratio conjugate
    private const float ApproachRadius = 0.98f;   // < MeleeRange (1.0f) so melee precondition passes

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>("Target", out var target, _entManager)
            || !_entManager.EntityExists(target))
            return (false, null);

        var self = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var angle = ((int)self * GoldenStride) % MathF.Tau;
        var offset = new Vector2(MathF.Cos(angle) * ApproachRadius, MathF.Sin(angle) * ApproachRadius);

        return (true, new Dictionary<string, object>
        {
            { "TargetCoordinates", new EntityCoordinates(target, offset) },
        });
    }
}
