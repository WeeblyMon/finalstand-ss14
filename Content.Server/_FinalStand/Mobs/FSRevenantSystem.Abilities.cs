using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Spawners;
using Robust.Shared.Physics.Components;

namespace Content.Server._FinalStand.Mobs;

public sealed partial class FSRevenantSystem
{
    private void ApplyAbilityDamage(EntityUid uid, FSRevenantComponent comp, EntityUid target, float amount)
    {
        var scaled = amount * GetDamageScale(uid);
        var pierced = scaled * Math.Clamp(comp.ResistanceBypass, 0f, 1f);
        var resisted = scaled - pierced;

        if (resisted > 0f)
        {
            var dmg = new DamageSpecifier();
            dmg.DamageDict["Slash"] = FixedPoint2.New(resisted);
            _damageable.TryChangeDamage(target, dmg, ignoreResistances: false, origin: uid);
        }

        if (pierced > 0f)
        {
            var dmg = new DamageSpecifier();
            dmg.DamageDict["Slash"] = FixedPoint2.New(pierced);
            _damageable.TryChangeDamage(target, dmg, ignoreResistances: true, origin: uid);
        }
    }

    private float GetDamageScale(EntityUid uid)
        => TryComp<FSWaveDamageScaleComponent>(uid, out var scale) ? scale.MeleeDamageMultiplier : 1f;

    private bool TryExecute(EntityUid uid, FSRevenantComponent comp)
    {
        if (!TryFindExecuteTarget(uid, comp, out var target, out var hpFraction))
        {
            _executesRefused++;
            return false;
        }

        comp.IsExecuting = true;
        comp.ExecuteWindupAccum = 0f;
        comp.ExecuteTarget = target;
        BeginChannel(uid);
        _effects.PlayExecuteWindup(uid);

        _effects.SpawnAttached(FSRevenantEffect.Scythe, target);

        Log.Debug($"[FSRevenant] {ToPrettyString(uid)} began execute on {ToPrettyString(target)} at {hpFraction:P0} hp");
        return true;
    }

    private bool TryFindExecuteTarget(EntityUid uid, FSRevenantComponent comp,
        out EntityUid best, out float bestFraction)
    {
        best = default;
        bestFraction = float.MaxValue;

        var myPos = _transform.GetMapCoordinates(uid);
        if (myPos.MapId == MapId.Nullspace)
            return false;

        _executeBuffer.Clear();
        _lookup.GetEntitiesInRange(myPos, comp.MeleeRange, _executeBuffer);

        var found = false;
        foreach (var (candidate, _) in _executeBuffer)
        {
            if (!IsExecutable(comp, candidate, out var fraction))
                continue;

            var preferred = fraction < bestFraction
                            || (Math.Abs(fraction - bestFraction) < 0.001f && comp.MarkedTarget == candidate);
            if (!preferred)
                continue;

            bestFraction = fraction;
            best = candidate;
            found = true;
        }

        return found;
    }

    private bool IsExecutable(FSRevenantComponent comp, EntityUid candidate, out float fraction)
    {
        fraction = float.MaxValue;

        if (!IsValidVictim(candidate))
            return false;
        if (!TryComp<DamageableComponent>(candidate, out var damageable))
            return false;
        if (!TryComp<MobThresholdsComponent>(candidate, out var mobThresh))
            return false;

        if (!_thresholds.TryGetThresholdForState(candidate, MobState.Critical, out var threshold, mobThresh)
            && !_thresholds.TryGetThresholdForState(candidate, MobState.Dead, out threshold, mobThresh))
            return false;

        if (threshold is not { } limit || limit <= 0)
            return false;

        fraction = 1f - damageable.TotalDamage.Float() / limit.Float();
        return fraction <= comp.ExecuteHealthThreshold;
    }

    private void FireExecute(EntityUid uid, FSRevenantComponent comp)
    {
        EndChannel(uid, comp);

        if (!comp.ExecuteTarget.HasValue || TerminatingOrDeleted(comp.ExecuteTarget.Value))
        {
            comp.ExecuteTarget = null;
            return;
        }

        var target = comp.ExecuteTarget.Value;
        comp.ExecuteTarget = null;

        var myPos = _transform.GetMapCoordinates(uid);
        var targetPos = _transform.GetMapCoordinates(target);

        if (targetPos.MapId != myPos.MapId
            || Vector2.Distance(myPos.Position, targetPos.Position) > comp.MeleeRange + comp.ExecuteEscapeTolerance)
        {
            Log.Debug($"[FSRevenant] {ToPrettyString(uid)} execute aborted, {ToPrettyString(target)} escaped");
            Replan(uid);
            return;
        }

        var dmg = new DamageSpecifier();
        dmg.DamageDict["Slash"] = FixedPoint2.New(comp.ExecuteDamage);
        _damageable.TryChangeDamage(target, dmg, ignoreResistances: true, origin: uid);

        _effects.SpawnAttached(FSRevenantEffect.Kill, target);
        _effects.PlayKillLaugh(target);

        var killed = new FSRevenantExecutedEvent(uid, target);
        RaiseLocalEvent(ref killed);
        _executesFired++;

        if (comp.MarkedTarget == target)
        {
            RemComp<FSRevenantMarkedComponent>(target);
            comp.MarkedTarget = null;
        }

        Replan(uid);
    }

    private bool TryBind(EntityUid uid, FSRevenantComponent comp, EntityUid target, float dist)
    {
        if (dist > comp.MeleeRange) return false;
        if (comp.BindAccum < comp.BindCooldown) return false;
        if (HasComp<FSRevenantBoundComponent>(target)) return false;

        comp.BindAccum = 0f;
        var bound = AddComp<FSRevenantBoundComponent>(target);
        bound.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(comp.BindDuration);
        Dirty(target, bound);

        ApplyAbilityDamage(uid, comp, target, comp.BindDamage);

        _effects.SpawnEffect(FSRevenantEffect.Bind, _transform.GetMapCoordinates(target));
        _effects.PlayBind(target);

        Log.Debug($"[FSRevenant] {ToPrettyString(uid)} bound {ToPrettyString(target)}");
        return true;
    }

    private bool TrySlice(EntityUid uid, FSRevenantComponent comp, EntityUid target, float dist,
        MapCoordinates myPos, MapCoordinates targetPos)
    {
        if (dist > comp.MeleeRange) return false;
        if (comp.SliceAccum < comp.SliceCooldown) return false;

        var facing = FacingBetween(myPos, targetPos);
        if (facing == null)
            return false;

        comp.SliceAccum = 0f;
        _effects.PlaySlice(uid);

        if (comp.UseVerticalNext)
            DoVerticalSlice(uid, comp, target, targetPos);
        else
            DoDiagonalSweep(uid, comp, target, myPos, facing.Value);

        comp.UseVerticalNext = !comp.UseVerticalNext;
        return true;
    }

    private void DoVerticalSlice(EntityUid uid, FSRevenantComponent comp, EntityUid target, MapCoordinates targetPos)
    {
        ApplyAbilityDamage(uid, comp, target, comp.SliceDamage);

        _effects.SpawnAttached(FSRevenantEffect.SliceVertical, target);
        _effects.SpawnAttached(FSRevenantEffect.Hit, target, delay: comp.SliceHitDelay);
    }

    private void DoDiagonalSweep(EntityUid uid, FSRevenantComponent comp, EntityUid target,
        MapCoordinates myPos, Vector2 facing)
    {
        _sweepBuffer.Clear();
        _lookup.GetEntitiesInRange(myPos, comp.SweepRange, _sweepBuffer);

        var cosHalfArc = MathF.Cos(comp.SweepArcDegrees * 0.5f * MathF.PI / 180f);

        foreach (var (targetUid, _) in _sweepBuffer)
        {
            if (!TryComp<MobStateComponent>(targetUid, out var ms) || ms.CurrentState != MobState.Alive)
                continue;

            var pos = _transform.GetMapCoordinates(targetUid);
            if (pos.MapId != myPos.MapId)
                continue;

            var toTarget = pos.Position - myPos.Position;
            if (toTarget.LengthSquared() > 0.001f
                && Vector2.Dot(Vector2.Normalize(toTarget), facing) < cosHalfArc)
                continue;

            ApplyAbilityDamage(uid, comp, targetUid, comp.SliceDamage);
            _effects.SpawnAttached(FSRevenantEffect.Hit, targetUid, delay: comp.SliceHitDelay);
        }

        _effects.SpawnAttached(FSRevenantEffect.SliceDiagonal, target);
    }

    private bool TryGrab(EntityUid uid, FSRevenantComponent comp, EntityUid htnTarget)
    {
        if (comp.GrabAccum < comp.GrabCooldown) return false;

        if (!TryResolveGrabTarget(uid, comp, htnTarget, out var target, out var myPos, out var targetPos))
            return false;

        var reach = FacingBetween(myPos, targetPos);
        if (reach == null)
            return false;

        comp.GrabAccum = 0f;
        comp.IsGrabPaused = true;
        comp.GrabPauseAccum = 0f;
        BeginChannel(uid);

        comp.GrabTarget = target;

        var grabbed = EnsureComp<FSRevenantGrabbedComponent>(target);
        grabbed.Puller = uid;
        grabbed.PullSpeed = comp.GrabPullSpeed;
        grabbed.StopRange = comp.GrabLandDistance;
        grabbed.EndsAt = _timing.CurTime + TimeSpan.FromSeconds(comp.GrabPauseDuration);
        Dirty(target, grabbed);

        var mid = new MapCoordinates((myPos.Position + targetPos.Position) / 2f, myPos.MapId);
        _effects.SpawnEffect(FSRevenantEffect.Grab, mid, reach.Value);

        _effects.PlayGrab(target);

        Log.Debug($"[FSRevenant] {ToPrettyString(uid)} grabbed {ToPrettyString(target)} " +
                  $"(marked={comp.MarkedTarget == target})");
        return true;
    }

    private void EndGrabPause(EntityUid uid, FSRevenantComponent comp)
    {
        LandGrab(uid, comp);
        comp.IsGrabPaused = false;
        EndChannel(uid, comp);
        Replan(uid);
    }

    private void LandGrab(EntityUid uid, FSRevenantComponent comp)
    {
        if (comp.GrabTarget is not { } victim)
            return;

        comp.GrabTarget = null;

        if (TerminatingOrDeleted(victim))
            return;
        if (TryComp<MobStateComponent>(victim, out var ms) && ms.CurrentState != MobState.Alive)
            return;

        var myPos = _transform.GetMapCoordinates(uid);
        var victimPos = _transform.GetMapCoordinates(victim);

        if (FacingBetween(myPos, victimPos) is not { } reach)
            return;

        if (Vector2.Distance(myPos.Position, victimPos.Position) > comp.GrabRange
            || !_examine.InRangeUnOccluded(uid, victim, comp.GrabRange, null))
        {
            Log.Debug($"[FSRevenant] {ToPrettyString(uid)} lost the grab on {ToPrettyString(victim)}");
            return;
        }

        ApplyAbilityDamage(uid, comp, victim, comp.GrabDamage);

        comp.BindAccum = comp.BindCooldown;
    }

    private bool TryBolt(EntityUid uid, FSRevenantComponent comp, EntityUid target, float dist,
        MapCoordinates myPos, MapCoordinates targetPos, bool pointBlank = false)
    {
        if (!pointBlank && dist <= comp.MeleeRange) return false;
        if (dist > comp.BoltMaxRange) return false;
        if (comp.BoltAccum < comp.BoltCooldown) return false;
        if (!_examine.InRangeUnOccluded(uid, target, comp.BoltMaxRange, null))
            return false;

        comp.BoltAccum = 0f;
        _effects.PlayBlast(uid);

        var baseAngle = MathF.Atan2(
            targetPos.Position.Y - myPos.Position.Y,
            targetPos.Position.X - myPos.Position.X);
        var halfSpread = comp.BoltSpreadDegrees * MathF.PI / 180f;
        var step = comp.BoltCount > 1 ? 2f * halfSpread / (comp.BoltCount - 1) : 0f;

        for (var i = 0; i < comp.BoltCount; i++)
        {
            var angle = comp.BoltCount > 1 ? baseAngle - halfSpread + step * i : baseAngle;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

            var bolt = Spawn(BoltProto, myPos);
            _transform.SetWorldRotation(bolt, new Angle(angle));
            if (TryComp<FSRevenantBoltComponent>(bolt, out var boltComp))
            {
                boltComp.Damage = comp.BoltDamage * GetDamageScale(uid);
                boltComp.ResistanceBypass = comp.ResistanceBypass;
                boltComp.Shooter = uid;
            }
            _physics.SetLinearVelocity(bolt, dir * comp.BoltSpeed);
        }

        Log.Debug($"[FSRevenant] {ToPrettyString(uid)} fired {comp.BoltCount} bolts at {ToPrettyString(target)}");
        return true;
    }

    private static Vector2? FacingBetween(MapCoordinates from, MapCoordinates to)
    {
        if (from.MapId == MapId.Nullspace || from.MapId != to.MapId)
            return null;

        var delta = to.Position - from.Position;
        return delta.LengthSquared() < 0.001f ? null : Vector2.Normalize(delta);
    }

    private void Replan(EntityUid uid)
    {
        if (TryComp<HTNComponent>(uid, out var htn))
            _htn.Replan(htn);
    }
}
