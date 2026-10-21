using System.Linq;
using System.Numerics;
using Content.Server._FinalStand.NPC;
using Content.Server.Examine;
using Content.Server.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Shared.Climbing;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.NPC;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using ClimbingComponent = Content.Shared.Climbing.Components.ClimbingComponent;
using Robust.Shared.Random;

namespace Content.Server.NPC.Systems;

public sealed partial class NPCSteeringSystem
{
    private void ApplySeek(Span<float> interest, Vector2 direction, float weight)
    {
        if (weight == 0f || direction == Vector2.Zero)
            return;

        var directionAngle = (float)direction.ToAngle().Theta;

        for (var i = 0; i < InterestDirections; i++)
        {
            var angle = i * InterestRadians;
            var dot = MathF.Cos(directionAngle - angle);
            dot = (dot + 1f) * 0.5f;
            interest[i] = Math.Clamp(interest[i] + dot * weight, 0f, 1f);
        }
    }

    #region Seek

    /// <summary>
    /// Takes into account agent-specific context that may allow it to bypass a node which is not FreeSpace.
    /// </summary>
    private bool IsFreeSpace(
        EntityUid uid,
        NPCSteeringComponent steering,
        PathPoly node)
    {
        if (node.Data.IsFreeSpace)
        {
            return true;
        }
        // Handle the case where the node is a climb, we can climb, and we are climbing.
        else if ((node.Data.Flags & PathfindingBreadcrumbFlag.Climb) != 0x0 &&
            (steering.Flags & PathFlags.Climbing) != 0x0 &&
            TryComp<ClimbingComponent>(uid, out var climbing) &&
            climbing.IsClimbing)
        {
            return true;
        }

        // TODO: Ideally for "FreeSpace" we check all entities on the tile and build flags dynamically (pathfinder refactor in future).
        var ents = _entSetPool.Get();
        _lookup.GetLocalEntitiesIntersecting(node.GraphUid, node.Box.Enlarged(-0.04f), ents, flags: LookupFlags.Static);
        var result = true;

        if (ents.Count > 0)
        {
            var fixtures = _fixturesQuery.GetComponent(uid);
            var physics = _physicsQuery.GetComponent(uid);

            foreach (var intersecting in ents)
            {
                if (!_physics.IsCurrentlyHardCollidable((uid, fixtures, physics), intersecting))
                {
                    continue;
                }

                result = false;
                break;
            }
        }

        _entSetPool.Return(ents);
        return result;
    }

    /// <summary>
    /// Attempts to head to the target destination, either via the next pathfinding node or the final target.
    /// </summary>
    private bool TrySeek(
        EntityUid uid,
        InputMoverComponent mover,
        NPCSteeringComponent steering,
        PhysicsComponent body,
        TransformComponent xform,
        Angle offsetRot,
        float moveSpeed,
        Span<float> interest,
        float frameTime,
        ref bool forceSteer)
    {
        var ourCoordinates = xform.Coordinates;
        var destinationCoordinates = steering.Coordinates;
        var inLos = true;

        // Check if we're in LOS if that's required.
        // TODO: Need something uhh better not sure on the interaction between these.
        if (!steering.ForceMove && steering.ArriveOnLineOfSight)
        {
            // TODO: use vision range
            inLos = _interaction.InRangeUnobstructed(uid, steering.Coordinates, 10f);

            if (inLos)
            {
                steering.LineOfSightTimer += frameTime;

                if (steering.LineOfSightTimer >= steering.LineOfSightTimeRequired)
                {
                    steering.Status = SteeringStatus.InRange;
                    ResetStuck(steering, ourCoordinates);
                    return true;
                }
            }
            else
            {
                steering.LineOfSightTimer = 0f;
            }
        }
        else
        {
            steering.LineOfSightTimer = 0f;
            steering.ForceMove = false;
        }

        // We've arrived, nothing else matters.
        if (xform.Coordinates.TryDistance(EntityManager, destinationCoordinates, out var targetDistance) &&
            inLos &&
            targetDistance <= steering.Range)
        {
            steering.Status = SteeringStatus.InRange;
            ResetStuck(steering, ourCoordinates);
            return true;
        }

        // Grab the target position, either the next path node or our end goal..
        var targetCoordinates = GetTargetCoordinates(steering);
        var needsPath = false;

        // FINALSTAND: a node the grid invalidated (a barricade broken or built, a door changed) means a new path,
        // not giving up the move. Vanilla returned NoPath before its own "get new ones" branch could run.
        if (!targetCoordinates.IsValid(EntityManager) && DropInvalidPath(steering))
        {
            targetCoordinates = GetTargetCoordinates(steering);
            needsPath = true;
            ResetStuck(steering, ourCoordinates);
        }

        if (!targetCoordinates.IsValid(EntityManager))
        {
            steering.Status = SteeringStatus.NoPath;
            return false;
        }

        // Check if mapids match.
        var targetMap = _transform.ToMapCoordinates(targetCoordinates);
        var ourMap = _transform.ToMapCoordinates(ourCoordinates);

        if (targetMap.MapId != ourMap.MapId)
        {
            steering.Status = SteeringStatus.NoPath;
            return false;
        }

        var direction = targetMap.Position - ourMap.Position;

        // FINALSTAND: a queued wave zombie (see stuck detection below) stands still until the one ahead moves on.
        if (steering.QueuedUntil != TimeSpan.Zero)
        {
            if (_timing.CurTime < steering.QueuedUntil)
                return false;

            if (IsQueuedBehindWave(uid, steering, ourMap, direction))
            {
                steering.QueuedUntil = _timing.CurTime + QueueHold;
                return false;
            }

            steering.QueuedUntil = TimeSpan.Zero;
            ResetStuck(steering, ourCoordinates);
        }

        // Need to be pretty close if it's just a node to make sure LOS for door bashes or the likes.
        bool arrived;

        if (targetCoordinates.Equals(steering.Coordinates))
        {
            // What's our tolerance for arrival.
            // If it's a pathfinding node it might be different to the destination.
            arrived = direction.Length() <= steering.Range;
        }
        // If next node is a free tile then get within its bounds.
        // This is to avoid popping it too early
        else if (steering.CurrentPath.TryPeek(out var node) && IsFreeSpace(uid, steering, node))
        {
            arrived = node.Box.Contains(ourCoordinates.Position);
        }
        // Try getting into blocked range I guess?
        // TODO: Consider melee range or the likes.
        else
        {
            arrived = direction.Length() <= SharedInteractionSystem.InteractionRange - 0.05f;
        }

        // Are we in range
        if (arrived)
        {
            // Node needs some kind of special handling like access or smashing.
            if (steering.CurrentPath.TryPeek(out var node) && !IsFreeSpace(uid, steering, node))
            {
                // Ignore stuck while handling obstacles.
                ResetStuck(steering, ourCoordinates);
                SteeringObstacleStatus status;

                // Breaking behaviours and the likes.
                lock (_obstacles)
                {
                    // FINALSTAND: smash-only mobs don't start a DoAfter so they don't need to stop first
                    var smashOnly = (steering.Flags & PathFlags.Smashing) != 0x0
                                    && (steering.Flags & PathFlags.Prying) == 0x0;
                    if (!smashOnly && body.LinearVelocity.LengthSquared() > 0.01f)
                    {
                        return true;
                    }

                    status = TryHandleFlags(uid, steering, node);
                }

                // TODO: Need to handle re-pathing in case the target moves around.
                switch (status)
                {
                    case SteeringObstacleStatus.Completed:
                        steering.DoAfterId = null;
                        break;
                    case SteeringObstacleStatus.Failed:
                        steering.DoAfterId = null;
                        // TODO: Blacklist the poly for next query
                        steering.Status = SteeringStatus.NoPath;
                        return false;
                    case SteeringObstacleStatus.Continuing:
                        CheckPath(uid, steering, xform, needsPath, targetDistance);
                        return true;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // Distance should already be handled above.
            // It was just a node, not the target, so grab the next destination (either the target or next node).
            if (steering.CurrentPath.Count > 0)
            {
                forceSteer = true;
                steering.CurrentPath.Dequeue();

                // Alright just adjust slightly and grab the next node so we don't stop moving for a tick.
                // TODO: If it's the last node just grab the target instead.
                targetCoordinates = GetTargetCoordinates(steering);

                // FINALSTAND: same for the node after this one.
                if (!targetCoordinates.IsValid(EntityManager) && DropInvalidPath(steering))
                {
                    targetCoordinates = GetTargetCoordinates(steering);
                    needsPath = true;
                }

                if (!targetCoordinates.IsValid(EntityManager))
                {
                    SetDirection(uid, mover, steering, Vector2.Zero);
                    steering.Status = SteeringStatus.NoPath;
                    return false;
                }

                targetMap = _transform.ToMapCoordinates(targetCoordinates);

                // Can't make it again.
                if (ourMap.MapId != targetMap.MapId)
                {
                    SetDirection(uid, mover, steering, Vector2.Zero);
                    steering.Status = SteeringStatus.NoPath;
                    return false;
                }

                // Gonna resume now business as usual
                direction = targetMap.Position - ourMap.Position;
                ResetStuck(steering, ourCoordinates);
            }
            else
            {
                needsPath = true;
            }
        }
        // Stuck detection
        // Check if we have moved further than the movespeed * stuck time.
        else if (AntiStuck &&
                 ourCoordinates.TryDistance(EntityManager, steering.LastStuckCoordinates, out var stuckDistance) &&
                 stuckDistance < NPCSteeringComponent.StuckDistance)
        {
            var stuckTime = _timing.CurTime - steering.LastStuckTime;
            // Either 1 second or how long it takes to move the stuck distance + buffer if we're REALLY slow.
            var maxStuckTime = Math.Max(1, NPCSteeringComponent.StuckDistance / moveSpeed * 1.2f);

            // FINALSTAND: a wave zombie waiting behind another one is queued, not stuck. A repath gives it the same
            // path, failing the move had the back rows of a horde replanning every few seconds, and pushing on
            // keeps the whole crowd awake in physics. It holds still instead and looks again shortly.
            if (stuckTime.TotalSeconds > maxStuckTime
                && _waveTagQuery.HasComponent(uid)
                && IsQueuedBehindWave(uid, steering, ourMap, direction))
            {
                ResetStuck(steering, ourCoordinates);
                steering.QueuedUntil = _timing.CurTime + QueueHold;
                return false;
            }

            if (stuckTime.TotalSeconds > maxStuckTime)
            {
                // TODO: Blacklist nodes (pathfinder factor wehn)
                // TODO: This should be a warning but
                // A) NPCs get stuck on non-anchored static bodies still (e.g. closets)
                // B) NPCs still try to move in locked containers (e.g. cow, hamster)
                // and I don't want to spam grafana even harder than it gets spammed rn.
                // FINALSTAND: the message was built (and the coordinates boxed) every tick even with debug logging off.
                if (Log.IsLogLevelEnabled(LogLevel.Debug))
                    Log.Debug($"NPC {ToPrettyString(uid)} found stuck at {ourCoordinates}");
                needsPath = true;

                if (stuckTime.TotalSeconds > maxStuckTime * 3)
                {
                    steering.Status = SteeringStatus.NoPath;
                    return false;
                }
            }
        }
        else
        {
            ResetStuck(steering, ourCoordinates);
        }

        // If not in LOS and no path then get a new one fam.
        if ((!inLos && steering.ArriveOnLineOfSight && steering.CurrentPath.Count == 0) ||
            (!steering.ArriveOnLineOfSight && steering.CurrentPath.Count == 0))
        {
            needsPath = true;
        }

        // TODO: Probably need partial planning support i.e. patch from the last node to where the target moved to.
        CheckPath(uid, steering, xform, needsPath, targetDistance);

        // If we don't have a path yet then do nothing; this is to avoid stutter-stepping if it turns out there's no path
        // available but we assume there was.
        if (steering is { Pathfind: true, CurrentPath.Count: 0 })
            return true;

        if (moveSpeed == 0f || direction == Vector2.Zero)
        {
            steering.Status = SteeringStatus.NoPath;
            return false;
        }

        var input = direction.Normalized();
        var tickMovement = moveSpeed * frameTime;

        // We have the input in world terms but need to convert it back to what movercontroller is doing.
        input = offsetRot.RotateVec(input);
        var norm = input.Normalized();
        var weight = MapValue(direction.Length(), tickMovement * 0.5f, tickMovement * 0.75f);

        ApplySeek(interest, norm, weight);

        // Prefer our current direction
        if (weight > 0f && body.LinearVelocity.LengthSquared() > 0f)
        {
            const float sameDirectionWeight = 0.1f;
            norm = body.LinearVelocity.Normalized();

            ApplySeek(interest, norm, sameDirectionWeight);
        }

        return true;
    }

    // FINALSTAND: the nodes after an invalidated one were planned around the old grid too, so the whole path goes.
    private static bool DropInvalidPath(NPCSteeringComponent steering)
    {
        if (!steering.CurrentPath.TryPeek(out var poly) || poly.IsValid())
            return false;

        steering.CurrentPath.Clear();
        return true;
    }

    private void ResetStuck(NPCSteeringComponent component, EntityCoordinates ourCoordinates)
    {
        component.LastStuckCoordinates = ourCoordinates;
        component.LastStuckTime = _timing.CurTime;
    }

    private void CheckPath(EntityUid uid, NPCSteeringComponent steering, TransformComponent xform, bool needsPath, float targetDistance)
    {
        if (!_pathfinding)
        {
            steering.CurrentPath.Clear();
            steering.PathfindToken?.Cancel();
            steering.PathfindToken = null;
            return;
        }

        // FINALSTAND: a requested repath waits for any request already in flight, which was planned before the change.
        if (steering.RepathRequested && !steering.Pathfind)
        {
            steering.RepathRequested = false;
            needsPath = true;
        }

        if (!needsPath && steering.CurrentPath.Count > 0)
        {
            needsPath = steering.CurrentPath.Count > 0 && (steering.CurrentPath.Peek().Data.Flags & PathfindingBreadcrumbFlag.Invalid) != 0x0;

            // If the target has sufficiently moved.
            // FINALSTAND: measured from where the destination was when the path was planned. Measuring from the
            // path's last node repathed every tick whenever Range was wider than RepathRange, because the
            // pathfinder stops at the first node within Range.
            var destination = _transform.ToMapCoordinates(steering.Coordinates, logError: false);

            if (destination.MapId != MapId.Nullspace &&
                destination.MapId == steering.PathPlannedFor.MapId &&
                (destination.Position - steering.PathPlannedFor.Position).Length() > steering.RepathRange)
            {
                needsPath = true;
            }
        }

        // Request the new path.
        if (needsPath)
        {
            RequestPath(uid, steering, xform, targetDistance);
        }
    }

    /// <summary>
    /// We may be pathfinding and moving at the same time in which case early nodes may be out of date.
    /// </summary>
    public void PrunePath(EntityUid uid, MapCoordinates mapCoordinates, Vector2 direction, List<PathPoly> nodes)
    {
        if (nodes.Count <= 1)
            return;

        // Work out if we're inside any nodes, then use the next one as the starting point.
        var index = 0;
        var found = false;

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var matrix = _transform.GetWorldMatrix(node.GraphUid);

            // Always want to prune the poly itself so we point to the next poly and don't backtrack.
            if (matrix.TransformBox(node.Box).Contains(mapCoordinates.Position))
            {
                index = i + 1;
                found = true;
                break;
            }
        }

        if (found)
        {
            nodes.RemoveRange(0, index);
            _pathfindingSystem.Simplify(nodes);
            return;
        }

        // Otherwise, take the node after the nearest node.

        // TODO: Really need layer support
        CollisionGroup mask = 0;

        if (TryComp<PhysicsComponent>(uid, out var physics))
        {
            mask = (CollisionGroup)physics.CollisionMask;
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            if (!node.Data.IsFreeSpace)
                break;

            var nodeMap = _transform.ToMapCoordinates(node.Coordinates);

            // If any nodes are 'behind us' relative to the target we'll prune them.
            // This isn't perfect but should fix most cases of stutter stepping.
            if (nodeMap.MapId == mapCoordinates.MapId &&
                Vector2.Dot(direction, nodeMap.Position - mapCoordinates.Position) < 0f)
            {
                nodes.RemoveAt(i);
                continue;
            }

            break;
        }

        _pathfindingSystem.Simplify(nodes);
    }

    /// <summary>
    /// Get the coordinates we should be heading towards.
    /// </summary>
    private EntityCoordinates GetTargetCoordinates(NPCSteeringComponent steering)
    {
        // Depending on what's going on we may return the target or a pathfind node.

        // Even if we're at the last node may not be able to head to target in case we get stuck on a corner or the likes.
        if (_pathfinding && steering.CurrentPath.Count >= 1 && steering.CurrentPath.TryPeek(out var nextTarget))
        {
            return GetCoordinates(nextTarget);
        }

        return steering.Coordinates;
    }

    /// <summary>
    /// Gets the fraction this value is between min and max
    /// </summary>
    /// <returns></returns>
    private float MapValue(float value, float minValue, float maxValue)
    {
        if (maxValue > minValue)
        {
            var mapped = (value - minValue) / (maxValue - minValue);
            return Math.Clamp(mapped, 0f, 1f);
        }

        return value >= minValue ? 1f : 0f;
    }

    #endregion

    #region Static Avoidance

    /// <summary>
    /// Tries to avoid static blockers such as walls.
    /// </summary>
    private void CollisionAvoidance(
        EntityUid uid,
        Angle offsetRot,
        Vector2 worldPos,
        float agentRadius,
        int layer,
        int mask,
        TransformComponent xform,
        Span<float> danger)
    {
        var objectRadius = 0.25f;
        var detectionRadius = MathF.Max(0.35f, agentRadius + objectRadius);
        var ents = _entSetPool.Get();
        _lookup.GetEntitiesInRange(uid, detectionRadius, ents, LookupFlags.Static | LookupFlags.Approximate);

        foreach (var ent in ents)
        {
            // TODO: If we can access the door or smth.
            if (!_physicsQuery.TryGetComponent(ent, out var otherBody) ||
                !otherBody.Hard ||
                !otherBody.CanCollide ||
                otherBody.BodyType == BodyType.KinematicController ||
                (mask & otherBody.CollisionLayer) == 0x0 &&
                (layer & otherBody.CollisionMask) == 0x0)
            {
                continue;
            }

            var xformB = _xformQuery.GetComponent(ent);

            if (!_physics.TryGetNearest(uid, ent,
                    out var pointA, out var pointB, out var distance,
                    xform, xformB))
            {
                continue;
            }

            if (distance > detectionRadius)
                continue;

            var weight = 1f;
            var obstacleDirection = pointB - pointA;

            // Inside each other so just use worldPos
            if (distance == 0f)
            {
                obstacleDirection = _transform.GetWorldPosition(xformB) - worldPos;
            }
            else
            {
                weight = (detectionRadius - distance) / detectionRadius;
            }

            if (obstacleDirection == Vector2.Zero)
                continue;

            obstacleDirection = offsetRot.RotateVec(obstacleDirection);
            var norm = obstacleDirection.Normalized();

            for (var i = 0; i < InterestDirections; i++)
            {
                var dot = Vector2.Dot(norm, Directions[i]);
                danger[i] = MathF.Max(dot * weight, danger[i]);
            }
        }

        _entSetPool.Return(ents);
    }

    #endregion

    #region Dynamic Avoidance

    /// <summary>
    /// Tries to avoid mobs of the same faction.
    /// </summary>
    private void Separation(
        EntityUid uid,
        Angle offsetRot,
        Vector2 worldPos,
        float agentRadius,
        int layer,
        int mask,
        PhysicsComponent body,
        TransformComponent xform,
        Span<float> danger)
    {
        var objectRadius = 0.25f;
        var detectionRadius = MathF.Max(0.35f, agentRadius + objectRadius);
        var ourVelocity = body.LinearVelocity;
        _factionQuery.TryGetComponent(uid, out var ourFaction);
        var ents = _entSetPool.Get();
        _lookup.GetEntitiesInRange(uid, detectionRadius, ents, LookupFlags.Dynamic | LookupFlags.Approximate);

        foreach (var ent in ents)
        {
            // TODO: If we can access the door or smth.
            if (!_physicsQuery.TryGetComponent(ent, out var otherBody) ||
                !otherBody.Hard ||
                !otherBody.CanCollide ||
                (mask & otherBody.CollisionLayer) == 0x0 &&
                (layer & otherBody.CollisionMask) == 0x0 ||
                !_factionQuery.TryGetComponent(ent, out var otherFaction) ||
                !_npcFaction.IsEntityFriendly((uid, ourFaction), (ent, otherFaction)) ||
                // Use <= 0 so we ignore stationary friends in case.
                Vector2.Dot(otherBody.LinearVelocity, ourVelocity) <= 0f)
            {
                continue;
            }

            var xformB = _xformQuery.GetComponent(ent);

            if (!_physics.TryGetNearest(uid, ent, out var pointA, out var pointB, out var distance, xform, xformB))
            {
                continue;
            }

            if (distance > detectionRadius)
                continue;

            var weight = 1f;
            var obstacleDirection = pointB - pointA;

            // Inside each other so just use worldPos
            if (distance == 0f)
            {
                obstacleDirection = _transform.GetWorldPosition(xformB) - worldPos;

                // Welp
                if (obstacleDirection == Vector2.Zero)
                {
                    obstacleDirection = _random.NextAngle().ToVec();
                }
            }
            else
            {
                weight = distance / detectionRadius;
            }

            obstacleDirection = offsetRot.RotateVec(obstacleDirection);
            var norm = obstacleDirection.Normalized();
            weight *= 0.25f;

            for (var i = 0; i < InterestDirections; i++)
            {
                var dot = Vector2.Dot(norm, Directions[i]);
                danger[i] = MathF.Max(dot * weight, danger[i]);
            }
        }

        _entSetPool.Return(ents);
    }

    #endregion

    #region Wave Zombie Spacing

    // FINALSTAND: wave zombie spacing, used for three things:
    //   - spacing from other zombies so they don't stack (symmetric, strongest at contact);
    //   - yielding to a zombie ahead going the same way (only the trailing one brakes, so they don't mirror-jink);
    //   - spreading out from a zombie alongside going the same way, so lanes fill the corridor width;
    // and standing off from living, standing non-zombies (players) so they settle at melee range.
    // Neighbours come from a grid of mob positions built once per tick. A broadphase query per zombie
    // was over half the server tick once a horde piled onto its targets.
    private const float PeerYieldRadius = 2.0f;

    // Heading is the normalised move input, or zero when standing still.
    private readonly record struct SpacingEntry(EntityUid Uid, Vector2 Position, Vector2 Heading, bool IsWave);

    private bool _spacingWanted;
    private readonly List<SpacingEntry> _spacingEntries = new();
    private readonly Dictionary<(MapId, Vector2i), List<int>> _spacingCells = new();

    // Cells filled this tick, the only ones that need clearing. The rest of the dictionary is ground nobody is standing
    // on and maps from earlier rounds; it is dropped once it outgrows the live cells by this much.
    private readonly List<List<int>> _spacingUsed = new();
    private readonly List<(MapId, Vector2i)> _spacingStale = new();
    private const int SpacingCellSlack = 4096;

    internal int SpacingCellCount => _spacingCells.Count;

    private void BuildSpacingIndex()
    {
        foreach (var cell in _spacingUsed)
            cell.Clear();
        _spacingUsed.Clear();
        _spacingEntries.Clear();

        var query = EntityQueryEnumerator<MobStateComponent, PhysicsComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var body, out var xform, out var meta))
        {
            if (body.BodyType == BodyType.Static
                || xform.MapID == MapId.Nullspace
                || (meta.Flags & MetaDataFlags.InContainer) != 0)
                continue;

            // A downed or dead body is walked over, otherwise every crit player becomes a wall and every corpse
            // a zombie to queue behind.
            if (mobState.CurrentState != MobState.Alive
                || _standingQuery.TryGetComponent(uid, out var standing) && !standing.Standing)
                continue;

            var isWave = _waveTagQuery.HasComponent(uid);

            var position = _transform.GetWorldPosition(xform);
            var moving = isWave && _inputMoverQuery.TryGetComponent(uid, out var mover)
                ? mover.CurTickSprintMovement
                : Vector2.Zero;

            var key = (xform.MapID, (position / PeerYieldRadius).Floored());
            if (!_spacingCells.TryGetValue(key, out var list))
            {
                list = new List<int>();
                _spacingCells[key] = list;
            }

            if (list.Count == 0)
                _spacingUsed.Add(list);

            list.Add(_spacingEntries.Count);
            var heading = moving == Vector2.Zero ? Vector2.Zero : moving.Normalized();
            _spacingEntries.Add(new SpacingEntry(uid, position, heading, isWave));
        }

        if (_spacingCells.Count > _spacingUsed.Count + SpacingCellSlack)
            PruneSpacingCells();
    }

    private void PruneSpacingCells()
    {
        foreach (var (key, cell) in _spacingCells)
        {
            if (cell.Count == 0)
                _spacingStale.Add(key);
        }

        foreach (var key in _spacingStale)
            _spacingCells.Remove(key);

        _spacingStale.Clear();
    }

    private void WaveZombieSpacing(
        EntityUid uid,
        MapId mapId,
        Angle offsetRot,
        Vector2 worldPos,
        Span<float> danger)
    {
        const float PeerSpacingRadius   = 1.2f;
        const float PeerSpacingWeight   = 0.6f;
        const float ForwardYieldWeight  = 0.45f;
        const float LateralYieldWeight  = 0.35f;
        const float LateralSpreadWeight = 0.45f;

        // Starts at melee range; weight above 1 so it beats seek interest up close.
        const float StandOffRadius = 1.0f;
        const float StandOffWeight = 2.5f;

        var isMoving = false;
        var myDirNorm = Vector2.Zero;
        if (_inputMoverQuery.TryGetComponent(uid, out var myMover) && myMover.CurTickSprintMovement != Vector2.Zero)
        {
            isMoving = true;
            myDirNorm = myMover.CurTickSprintMovement.Normalized();
        }

        var preferLeft = ((int) uid & 1) == 0;
        var centre = (worldPos / PeerYieldRadius).Floored();

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (!_spacingCells.TryGetValue((mapId, centre + new Vector2i(x, y)), out var cell))
                    continue;

                foreach (var i in cell)
                {
                    var other = _spacingEntries[i];
                    if (other.Uid == uid)
                        continue;

                    var toOther = other.Position - worldPos;
                    var distSq = toOther.LengthSquared();
                    if (distSq < 0.0001f || distSq > PeerYieldRadius * PeerYieldRadius)
                        continue;

                    var dist = MathF.Sqrt(distSq);
                    var toOtherDir = toOther / dist;
                    var towardLocal = offsetRot.RotateVec(toOtherDir);

                    if (!other.IsWave)
                    {
                        if (dist <= StandOffRadius)
                            AddDanger(danger, towardLocal, StandOffWeight * (1f - dist / StandOffRadius));
                        continue;
                    }

                    if (dist < PeerSpacingRadius)
                        AddDanger(danger, towardLocal, PeerSpacingWeight * (1f - dist / PeerSpacingRadius));

                    if (!isMoving
                        || other.Heading == Vector2.Zero
                        || Vector2.Dot(myDirNorm, other.Heading) <= 0.4f)
                        continue;

                    if (Vector2.Dot(myDirNorm, toOtherDir) <= 0.4f)
                    {
                        AddDanger(danger, towardLocal, LateralSpreadWeight);
                        continue;
                    }

                    // Ahead of us and going our way. Only the higher-uid zombie yields.
                    if ((int) other.Uid >= (int) uid)
                        continue;

                    AddDanger(danger, towardLocal, ForwardYieldWeight);

                    // Commit to a fixed side so the pass doesn't flip-flop.
                    var lateral = preferLeft
                        ? new Vector2(-toOtherDir.Y, toOtherDir.X)
                        : new Vector2(toOtherDir.Y, -toOtherDir.X);
                    AddDanger(danger, offsetRot.RotateVec(-lateral), LateralYieldWeight);
                }
            }
        }
    }

    private static readonly TimeSpan QueueHold = TimeSpan.FromSeconds(0.5);

    // Another wave zombie close ahead and nearer our target means we're waiting our turn. One that is moving
    // only counts if it's after the same target, so two zombies can never each be waiting on the other.
    private bool IsQueuedBehindWave(EntityUid uid, NPCSteeringComponent steering, MapCoordinates ourMap, Vector2 direction)
    {
        const float QueueRadius = 1.0f;
        const float AheadCos = 0.5f;

        var length = direction.Length();
        var target = steering.Coordinates.EntityId;
        if (length < 0.01f
            || !_xformQuery.TryGetComponent(target, out var targetXform)
            || targetXform.MapID != ourMap.MapId)
            return false;

        var targetPos = _transform.GetWorldPosition(targetXform);
        var ourDistSq = (ourMap.Position - targetPos).LengthSquared();
        var heading = direction / length;
        var centre = (ourMap.Position / PeerYieldRadius).Floored();

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (!_spacingCells.TryGetValue((ourMap.MapId, centre + new Vector2i(x, y)), out var cell))
                    continue;

                foreach (var i in cell)
                {
                    var other = _spacingEntries[i];
                    if (!other.IsWave || other.Uid == uid)
                        continue;

                    var toOther = other.Position - ourMap.Position;
                    var dist = toOther.Length();
                    if (dist < 0.01f
                        || dist > QueueRadius
                        || Vector2.Dot(heading, toOther) <= AheadCos * dist
                        || (other.Position - targetPos).LengthSquared() >= ourDistSq)
                        continue;

                    if (!_steeringQuery.TryGetComponent(other.Uid, out var otherSteering)
                        || otherSteering.Coordinates.EntityId == target)
                        return true;
                }
            }
        }

        return false;
    }

    private static void AddDanger(Span<float> danger, Vector2 towardLocal, float weight)
    {
        for (var i = 0; i < InterestDirections; i++)
        {
            var dot = Vector2.Dot(towardLocal, Directions[i]);
            if (dot > 0f)
                danger[i] = MathF.Max(danger[i], dot * weight);
        }
    }

    #endregion

    // TODO: Alignment

    // TODO: Cohesion
    private void Blend(NPCSteeringComponent steering, float frameTime, Span<float> interest, Span<float> danger)
    {
        /*
         * Future sloth notes:
         * Pathfinder cleanup:
            - Cleanup whatever the fuck is happening in pathfinder
            - Use Flee for melee behavior / actions and get the seek direction from that rather than bulldozing
            - Must always have a path
            - Path should return the full version + the snipped version
            - Pathfinder needs to do diagonals
            - Next node is either <current node + 1> or <nearest node + 1> (on the full path)
            - If greater than <1.5m distance> repath
         */

        // IDK why I didn't do this sooner but blending is a lot better than lastdir for fixing stuttering.
        const float BlendWeight = 10f;
        var blendValue = Math.Min(1f, frameTime * BlendWeight);

        for (var i = 0; i < InterestDirections; i++)
        {
            var currentInterest = interest[i];
            var lastInterest = steering.Interest[i];
            var interestDiff = (currentInterest - lastInterest) * blendValue;
            steering.Interest[i] = lastInterest + interestDiff;

            var currentDanger = danger[i];
            var lastDanger = steering.Danger[i];
            var dangerDiff = (currentDanger - lastDanger) * blendValue;
            steering.Danger[i] = lastDanger + dangerDiff;
        }
    }
}
