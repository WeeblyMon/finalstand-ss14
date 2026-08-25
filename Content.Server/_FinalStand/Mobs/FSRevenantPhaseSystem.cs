using System.Numerics;
using Content.Server.NPC.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Mobs;

public sealed partial class FSRevenantPhaseSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private FSRevenantSystem _revenant = default!;
    [Dependency] private IGameTiming _timing = default!;

    public const float HoldBand = 0.75f;

    private const float ApproachEaseRange = 3f;
    private const float MinApproachThrottle = 0.35f;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(NPCSteeringSystem));
        UpdatesAfter.Add(typeof(FSRevenantSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSRevenantPhasingComponent, InputMoverComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var phasing, out var mover, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;

            if (!_blocker.CanMove(uid))
                continue;

            var worldDir = GetSeekDirection(uid, phasing);
            var offsetRot = -_mover.GetParentGridAngle(mover);

            mover.CurTickSprintMovement = worldDir == Vector2.Zero
                ? Vector2.Zero
                : offsetRot.RotateVec(worldDir);
            mover.LastInputTick = _timing.CurTick;
            mover.LastInputSubTick = ushort.MaxValue;
        }
    }

    private Vector2 GetSeekDirection(EntityUid uid, FSRevenantPhasingComponent phasing)
    {
        if (!TryComp<FSRevenantComponent>(uid, out var revenant) || revenant.CurrentTarget is not { } target)
            return Vector2.Zero;

        var retreating = _revenant.IsRetreating(revenant);

        if (TerminatingOrDeleted(target))
            return Vector2.Zero;

        var myPos = _transform.GetMapCoordinates(uid);
        var targetPos = _transform.GetMapCoordinates(target);

        if (myPos.MapId == MapId.Nullspace || myPos.MapId != targetPos.MapId)
            return Vector2.Zero;

        var delta = targetPos.Position - myPos.Position;
        var distance = delta.Length();

        if (distance < 0.01f)
            return Vector2.Zero;

        if (retreating)
            return distance >= _revenant.GetRetreatRange(revenant) ? Vector2.Zero : -(delta / distance);

        var hold = _revenant.GetHoldDistance(revenant, phasing);
        var dir = delta / distance;

        if (distance > hold + HoldBand)
        {
            var overshoot = distance - (hold + HoldBand);
            var throttle = Math.Clamp(overshoot / ApproachEaseRange, MinApproachThrottle, 1f);
            return dir * throttle;
        }

        if (distance < hold - HoldBand)
            return -dir * MinApproachThrottle;

        if (!_revenant.IsStalking(revenant))
            return Vector2.Zero;

        return revenant.OrbitClockwise
            ? new Vector2(dir.Y, -dir.X)
            : new Vector2(-dir.Y, dir.X);
    }
}
