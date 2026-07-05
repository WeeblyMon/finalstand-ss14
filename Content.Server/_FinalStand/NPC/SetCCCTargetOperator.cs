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

    private FSSlotRingSystem _slotRing = default!;

    // Must be < MeleeRange (1.0f) so the planning-phase TargetInRangePrecondition passes.
    private const float ApproachRadius = 0.9f;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _slotRing = sysManager.GetEntitySystem<FSSlotRingSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(NPCBlackboard.CurrentOrderedTarget, out var target, _entManager)
            || !_entManager.EntityExists(target))
            return (false, null);

        var self = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var slotDir = _slotRing.GetOrAssignSlot(self, target);
        var offset = slotDir.LengthSquared() > 0f
            ? slotDir.Normalized() * ApproachRadius
            : new Vector2(ApproachRadius, 0f);

        return (true, new Dictionary<string, object>
        {
            { "Target", target },
            { "TargetCoordinates", new EntityCoordinates(target, offset) },
        });
    }
}
