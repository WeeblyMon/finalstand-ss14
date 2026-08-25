using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Mobs;

public sealed partial class FSRevenantSystem
{
    private void ExpireMarks()
    {
        var now = _timing.CurTime;
        _expiredMarks.Clear();

        var query = EntityQueryEnumerator<FSRevenantMarkedComponent>();
        while (query.MoveNext(out var uid, out var mark))
        {
            if (now >= mark.ExpiresAt)
                _expiredMarks.Add(uid);
        }

        foreach (var uid in _expiredMarks)
            RemComp<FSRevenantMarkedComponent>(uid);
    }

    private void TryMark(EntityUid uid, FSRevenantComponent comp)
    {
        if (comp.MarkedTarget.HasValue
            && !TerminatingOrDeleted(comp.MarkedTarget.Value)
            && TryComp<FSRevenantMarkedComponent>(comp.MarkedTarget.Value, out var existing)
            && _timing.CurTime < existing.ExpiresAt)
        {
            return;
        }

        var isolated = FindMarkTarget(uid, comp);
        if (isolated == null)
            return;

        if (comp.MarkedTarget.HasValue && !TerminatingOrDeleted(comp.MarkedTarget.Value))
            RemComp<FSRevenantMarkedComponent>(comp.MarkedTarget.Value);

        var mark = EnsureComp<FSRevenantMarkedComponent>(isolated.Value);
        mark.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(comp.MarkDuration);
        mark.MarkedByRevenant = uid;
        Dirty(isolated.Value, mark);
        comp.MarkedTarget = isolated.Value;
        comp.MarkedAt = _timing.CurTime;
        _marksPlaced++;

        _effects.SpawnAttached(FSRevenantEffect.Mark, isolated.Value, Vector2.Zero);
        _effects.PlayMark(isolated.Value);

        _popup.PopupEntity(Loc.GetString("fs-revenant-marked"), isolated.Value, isolated.Value, PopupType.LargeCaution);

        Log.Debug($"[FSRevenant] {ToPrettyString(uid)} marked {ToPrettyString(isolated.Value)}");
    }

    private EntityUid? FindMarkTarget(EntityUid seeker, FSRevenantComponent comp)
    {
        var origin = _transform.GetMapCoordinates(seeker);
        if (origin.MapId == MapId.Nullspace)
            return null;

        var cccPos = TryGetCccPosition(origin.MapId);

        _candidateBuffer.Clear();
        var query = EntityQueryEnumerator<ActorComponent>();
        while (query.MoveNext(out var candidateUid, out _))
        {
            if (HasComp<WaveSpawnedTagComponent>(candidateUid)) continue;
            if (HasComp<GhostComponent>(candidateUid)) continue;
            if (TryComp<MobStateComponent>(candidateUid, out var ms) && ms.CurrentState != MobState.Alive) continue;

            var pos = _transform.GetMapCoordinates(candidateUid);
            if (pos.MapId != origin.MapId) continue;

            var seekerDist = Vector2.Distance(origin.Position, pos.Position);
            if (seekerDist > comp.MarkSearchRange) continue;

            _candidateBuffer.Add((candidateUid, pos.Position, seekerDist));
        }

        if (_candidateBuffer.Count == 0)
            return null;

        var maxCccDist = 0f;
        if (cccPos is { } ccc)
        {
            foreach (var candidate in _candidateBuffer)
                maxCccDist = MathF.Max(maxCccDist, Vector2.Distance(ccc, candidate.Pos));
        }

        EntityUid? best = null;
        var bestScore = float.MinValue;
        var bestDist = float.MaxValue;
        var isolationSq = comp.MarkIsolationRadius * comp.MarkIsolationRadius;

        for (var i = 0; i < _candidateBuffer.Count; i++)
        {
            var candidate = _candidateBuffer[i];

            var allyCount = 0;
            for (var j = 0; j < _candidateBuffer.Count; j++)
            {
                if (i == j) continue;
                if (Vector2.DistanceSquared(candidate.Pos, _candidateBuffer[j].Pos) <= isolationSq)
                    allyCount++;
            }

            var isolationScore = 1f / (1f + allyCount);

            var backlineScore = 0f;
            if (cccPos is { } cccOrigin && maxCccDist > 0f)
                backlineScore = 1f - Math.Clamp(Vector2.Distance(cccOrigin, candidate.Pos) / maxCccDist, 0f, 1f);

            var score = backlineScore * comp.MarkBacklineWeight + isolationScore * comp.MarkIsolationWeight;

            if (TryComp<FSRevenantMarkedComponent>(candidate.Uid, out var existing)
                && existing.MarkedByRevenant != seeker)
                score -= 1f;

            if (score > bestScore || (MathHelper.CloseTo(score, bestScore) && candidate.SeekerDist < bestDist))
            {
                bestScore = score;
                bestDist = candidate.SeekerDist;
                best = candidate.Uid;
            }
        }

        return best;
    }

    private Vector2? TryGetCccPosition(MapId mapId)
    {
        if (_cccEntity is not { } ccc || TerminatingOrDeleted(ccc))
            return null;

        var pos = _transform.GetMapCoordinates(ccc);
        return pos.MapId == mapId ? pos.Position : null;
    }

    private bool TryResolveGrabTarget(EntityUid uid, FSRevenantComponent comp, EntityUid htnTarget,
        out EntityUid target, out MapCoordinates myPos, out MapCoordinates targetPos)
    {
        myPos = _transform.GetMapCoordinates(uid);
        targetPos = default;
        target = default;

        if (myPos.MapId == MapId.Nullspace)
            return false;

        if (comp.MarkedTarget is { } marked && !TerminatingOrDeleted(marked)
            && IsGrabbable(uid, comp, marked, myPos, out targetPos))
        {
            target = marked;
            return true;
        }

        if (IsGrabbable(uid, comp, htnTarget, myPos, out targetPos))
        {
            target = htnTarget;
            return true;
        }

        return false;
    }

    private bool IsGrabbable(EntityUid uid, FSRevenantComponent comp, EntityUid candidate,
        MapCoordinates myPos, out MapCoordinates candidatePos)
    {
        candidatePos = default;

        if (!IsValidVictim(candidate))
            return false;

        candidatePos = _transform.GetMapCoordinates(candidate);
        if (candidatePos.MapId != myPos.MapId)
            return false;

        var dist = Vector2.Distance(myPos.Position, candidatePos.Position);
        if (dist < comp.GrabMinRange || dist > comp.GrabRange)
            return false;

        return _examine.InRangeUnOccluded(uid, candidate, comp.GrabRange, null);
    }
}
