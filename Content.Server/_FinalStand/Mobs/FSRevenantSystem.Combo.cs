using System.Numerics;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.Mobs;

public sealed partial class FSRevenantSystem
{
    private void RunCombo(EntityUid uid, FSRevenantComponent comp, bool isDarkWave, EntityUid target,
        float dist, MapCoordinates myPos, MapCoordinates targetPos, float frameTime)
    {
        comp.PhaseAccum += frameTime;

        switch (comp.Phase)
        {
            case FSRevenantPhase.Stalk:
                TickStalk(uid, comp, isDarkWave, target, dist);
                return;

            case FSRevenantPhase.Retreat:
                var duration = isDarkWave ? comp.DarkWaveRetreatDuration : comp.RetreatDuration;
                var minDuration = isDarkWave ? comp.DarkWaveRetreatMinDuration : comp.RetreatMinDuration;

                if (comp.PhaseAccum >= duration
                    || (comp.PhaseAccum >= minDuration && dist >= GetRetreatRange(comp)))
                    EnterPhase(uid, comp, FSRevenantPhase.Stalk);
                return;
        }

        if (comp.GcdAccum < comp.GlobalCooldown)
            return;

        var fired = comp.Phase switch
        {
            FSRevenantPhase.Grab => TryGrab(uid, comp, target),
            FSRevenantPhase.Bind => TryBind(uid, comp, target, dist),
            FSRevenantPhase.Volley => TryBolt(uid, comp, target, dist, myPos, targetPos, pointBlank: true),
            FSRevenantPhase.SliceOne or FSRevenantPhase.SliceTwo =>
                TrySlice(uid, comp, target, dist, myPos, targetPos),
            FSRevenantPhase.Execute => TryExecute(uid, comp),
            _ => false,
        };

        if (fired)
        {
            comp.GcdAccum = 0f;
            AdvancePhase(uid, comp, isDarkWave);
            return;
        }

        if (comp.Phase == FSRevenantPhase.Execute || comp.PhaseAccum >= comp.ComboStepTimeout)
            AdvancePhase(uid, comp, isDarkWave);
    }

    private void TickStalk(EntityUid uid, FSRevenantComponent comp, bool isDarkWave, EntityUid target, float dist)
    {
        if (comp.GcdAccum < comp.GlobalCooldown)
            return;

        if (IsTargetEngaged(uid, target))
            return;

        var openerReady = isDarkWave
            ? comp.SliceAccum >= comp.SliceCooldown
            : comp.GrabAccum >= comp.GrabCooldown
              && comp.MarkedTarget == target
              && _timing.CurTime >= comp.MarkedAt + TimeSpan.FromSeconds(comp.MarkGraceDuration);

        if (openerReady && dist <= comp.EngageRange)
        {
            _combosOpened++;
            EnterPhase(uid, comp, isDarkWave ? FSRevenantPhase.SliceOne : FSRevenantPhase.Grab);
        }
    }

    private bool IsTargetEngaged(EntityUid self, EntityUid target)
    {
        var query = EntityQueryEnumerator<FSRevenantComponent, MobStateComponent>();
        while (query.MoveNext(out var other, out var otherComp, out var otherState))
        {
            if (other == self || otherComp.CurrentTarget != target)
                continue;

            if (otherState.CurrentState != MobState.Alive)
                continue;

            if (otherComp.Phase != FSRevenantPhase.Stalk && otherComp.Phase != FSRevenantPhase.Retreat)
                return true;
        }

        return false;
    }

    private void AdvancePhase(EntityUid uid, FSRevenantComponent comp, bool isDarkWave)
    {
        var next = comp.Phase switch
        {
            FSRevenantPhase.Grab => FSRevenantPhase.Bind,
            FSRevenantPhase.Bind => FSRevenantPhase.Volley,
            FSRevenantPhase.Volley => FSRevenantPhase.SliceOne,
            FSRevenantPhase.SliceOne => isDarkWave ? FSRevenantPhase.Execute : FSRevenantPhase.SliceTwo,
            FSRevenantPhase.SliceTwo => FSRevenantPhase.Execute,
            _ => FSRevenantPhase.Retreat,
        };

        if (next == FSRevenantPhase.Retreat)
            _combosCompleted++;

        EnterPhase(uid, comp, next);
    }

    private void EnterPhase(EntityUid uid, FSRevenantComponent comp, FSRevenantPhase phase)
    {
        if (phase == FSRevenantPhase.Retreat && comp.Phase != FSRevenantPhase.Retreat)
            _effects.PlayVanish(uid);

        comp.Phase = phase;
        comp.PhaseAccum = 0f;
    }

    public void ForceRetreat(EntityUid uid)
    {
        if (TryComp<FSRevenantComponent>(uid, out var comp))
            EnterPhase(uid, comp, FSRevenantPhase.Retreat);
    }

    public bool IsDarkWave => _isDarkWave;

    public bool IsRetreating(FSRevenantComponent comp)
        => comp.Phase == FSRevenantPhase.Retreat;

    public bool IsStalking(FSRevenantComponent comp)
        => comp.Phase == FSRevenantPhase.Stalk;

    public float GetRetreatRange(FSRevenantComponent comp)
        => _isDarkWave ? comp.DarkWaveRetreatRange : comp.RetreatRange;

    public float GetHoldDistance(FSRevenantComponent comp, FSRevenantPhasingComponent phasing)
        => comp.Phase switch
        {
            FSRevenantPhase.Stalk => comp.StalkRange,
            FSRevenantPhase.Grab => (comp.GrabMinRange + comp.GrabRange) * 0.5f,
            _ => phasing.StopRange,
        };
}
