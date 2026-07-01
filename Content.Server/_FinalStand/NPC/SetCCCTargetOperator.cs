using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.NPC;

public sealed partial class SetCCCTargetOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    // Must be strictly less than MeleeRange (default 1.0). MoveToOperator lies during planning
    // by writing OwnerCoordinates=TargetCoordinates as an effect; the follow-up MeleeOperator's
    // TargetInRangePrecondition then checks distance from that spoofed position to the target
    // entity using strict `<`. Any offset magnitude ≥ 1.0 (including 1.0 ± FP error) fails the
    // check and drops the whole CCCBeeline branch into IdleCompound.
    private const float ApproachRadius = 0.9f;

    // 2π * (1 - 1/φ) — golden-ratio conjugate stride. Consecutive UIDs land on quasi-uniformly
    // distributed angles around the ring, so a wave of zombies fans out around CCC instead of
    // stacking on one tile. Deterministic (no blackboard state), survives HTN replans.
    private const float GoldenAngle = 2.399963f;

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(NPCBlackboard.CurrentOrderedTarget, out var target, _entManager)
            || !_entManager.EntityExists(target))
            return (false, null);

        var self = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var angle = (int)self * GoldenAngle % MathF.Tau;
        var offset = new Vector2(MathF.Cos(angle) * ApproachRadius, MathF.Sin(angle) * ApproachRadius);

        return (true, new Dictionary<string, object>
        {
            { "Target", target },
            { "TargetCoordinates", new EntityCoordinates(target, offset) },
        });
    }
}
