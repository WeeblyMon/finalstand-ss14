using System.Numerics;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Mobs;

public sealed partial class FSRevenantSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FSRevenantEffectSystem _effects = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;

    private int _combosOpened;
    private int _combosCompleted;
    private int _executesFired;
    private int _executesRefused;
    private int _marksPlaced;

    private const string BoltProto = "FSRevenantBoltProjectile";
    private const string HtnMeleeRangeKey = "MeleeRange";
    private const string HtnTargetKey = "Target";

    private FSRevenantAbility? _debugLock;
    private bool _isDarkWave;
    private EntityUid? _cccEntity;

    private readonly HashSet<Entity<FSFriendlyFireComponent>> _sweepBuffer = new();
    private readonly List<(EntityUid Uid, Vector2 Pos, float SeekerDist)> _candidateBuffer = new();
    private readonly List<EntityUid> _expiredMarks = new();
    private readonly HashSet<Entity<FSFriendlyFireComponent>> _executeBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRevenantComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FSRevenantComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FSRevenantComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMapInit(EntityUid uid, FSRevenantComponent comp, MapInitEvent args)
    {
        comp.GrabAccum = comp.GrabCooldown;
        comp.BindAccum = comp.BindCooldown;
        comp.SliceAccum = comp.SliceCooldown;
        comp.BoltAccum = comp.BoltCooldown;
        comp.GcdAccum = comp.GlobalCooldown;
        comp.MarkAccum = comp.MarkScanCooldown;
        comp.OrbitClockwise = _random.Prob(0.5f);

        if (TryComp<HTNComponent>(uid, out var htn))
            htn.Blackboard.SetValue(HtnMeleeRangeKey, comp.MeleeRange);

        comp.LockedAbility = _debugLock;
    }

    public int SetDebugLock(FSRevenantAbility? ability)
    {
        _debugLock = ability;

        var count = 0;
        var query = EntityQueryEnumerator<FSRevenantComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            comp.LockedAbility = ability;
            comp.LastLockedOutcome = null;
            count++;
        }

        return count;
    }

    private void OnShutdown(EntityUid uid, FSRevenantComponent comp, ComponentShutdown args)
    {
        EndChannel(uid, comp);
    }

    private void OnMobStateChanged(EntityUid uid, FSRevenantComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        EndChannel(uid, comp);

        if (!comp.DeathAnnounced)
        {
            comp.DeathAnnounced = true;
            _effects.SpawnEffect(FSRevenantEffect.Death, _transform.GetMapCoordinates(uid));
        }

        if (comp.MarkedTarget.HasValue && !TerminatingOrDeleted(comp.MarkedTarget.Value))
            RemComp<FSRevenantMarkedComponent>(comp.MarkedTarget.Value);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        ExpireMarks();

        var isDarkWave = _isDarkWave = ReadWaveState();

        var query = EntityQueryEnumerator<FSRevenantComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;

            comp.GcdAccum += frameTime;
            comp.MarkAccum += frameTime;
            comp.GrabAccum += frameTime;
            comp.BindAccum += frameTime;
            comp.SliceAccum += frameTime;
            comp.BoltAccum += frameTime;

            if (TickChannel(uid, comp, frameTime))
                continue;

            ExpireStaleMark(comp);

            var canMark = comp.Phase is FSRevenantPhase.Stalk or FSRevenantPhase.Retreat;
            if (canMark && comp.MarkedTarget == null && comp.MarkAccum >= comp.MarkRescanDelay)
            {
                comp.MarkAccum = 0f;
                TryMark(uid, comp);
            }

            if (!TryResolveTarget(uid, comp, out var target, out var myPos, out var targetPos))
            {
                if (comp.Phase != FSRevenantPhase.Stalk)
                    EnterPhase(uid, comp, FSRevenantPhase.Stalk);
                continue;
            }

            var dist = Vector2.Distance(myPos.Position, targetPos.Position);

            if (comp.LockedAbility is { } locked)
            {
                if (comp.GcdAccum < comp.GlobalCooldown)
                    continue;

                var lockFired = DispatchLocked(uid, comp, locked, target, dist, myPos, targetPos);

                if (comp.LastLockedOutcome != lockFired)
                {
                    comp.LastLockedOutcome = lockFired;
                    Log.Info($"[FSRevenant] {ToPrettyString(uid)} locked to {locked}: " +
                             $"{(lockFired ? "firing" : $"refused at {dist:F1} tiles")}");
                }

                if (lockFired)
                    comp.GcdAccum = 0f;
                continue;
            }

            RunCombo(uid, comp, isDarkWave, target, dist, myPos, targetPos, frameTime);
        }
    }

    private bool DispatchLocked(EntityUid uid, FSRevenantComponent comp, FSRevenantAbility ability,
        EntityUid target, float dist, MapCoordinates myPos, MapCoordinates targetPos)
    {
        switch (ability)
        {
            case FSRevenantAbility.Execute:
                return TryExecute(uid, comp);
            case FSRevenantAbility.Grab:
                comp.GrabAccum = comp.GrabCooldown;
                return TryGrab(uid, comp, target);
            case FSRevenantAbility.Bind:
                comp.BindAccum = comp.BindCooldown;
                return TryBind(uid, comp, target, dist);
            case FSRevenantAbility.Slice:
                comp.SliceAccum = comp.SliceCooldown;
                return TrySlice(uid, comp, target, dist, myPos, targetPos);
            case FSRevenantAbility.Bolt:
                comp.BoltAccum = comp.BoltCooldown;
                return TryBolt(uid, comp, target, dist, myPos, targetPos);
            default:
                return false;
        }
    }

    private bool TickChannel(EntityUid uid, FSRevenantComponent comp, float frameTime)
    {
        if (comp.IsExecuting)
        {
            comp.ExecuteWindupAccum += frameTime;
            if (comp.ExecuteWindupAccum >= comp.ExecuteWindupDuration)
                FireExecute(uid, comp);
            return true;
        }

        if (comp.IsGrabPaused)
        {
            comp.GrabPauseAccum += frameTime;
            if (comp.GrabPauseAccum >= comp.GrabPauseDuration)
                EndGrabPause(uid, comp);
            return true;
        }

        return false;
    }

    private void ExpireStaleMark(FSRevenantComponent comp)
    {
        if (!comp.MarkedTarget.HasValue)
            return;

        var marked = comp.MarkedTarget.Value;
        if (TerminatingOrDeleted(marked) || !HasComp<FSRevenantMarkedComponent>(marked))
            comp.MarkedTarget = null;
    }

    private bool TryResolveTarget(EntityUid uid, FSRevenantComponent comp, out EntityUid target,
        out MapCoordinates myPos, out MapCoordinates targetPos)
    {
        target = default;
        targetPos = default;
        myPos = _transform.GetMapCoordinates(uid);

        if (myPos.MapId == MapId.Nullspace)
        {
            comp.CurrentTarget = null;
            return false;
        }

        if (comp.MarkedTarget is { } marked && IsPursuable(comp, marked, myPos, out targetPos, ignoreRange: true))
        {
            target = marked;
            comp.CurrentTarget = target;
            return true;
        }

        if (TryComp<HTNComponent>(uid, out var htn)
            && htn.Blackboard.TryGetValue(HtnTargetKey, out EntityUid? resolved, EntityManager)
            && resolved != null
            && IsPursuable(comp, resolved.Value, myPos, out targetPos))
        {
            target = resolved.Value;
            comp.CurrentTarget = target;
            return true;
        }

        if (TryAcquireTarget(uid, comp, myPos, out target, out targetPos))
        {
            comp.CurrentTarget = target;
            return true;
        }

        comp.CurrentTarget = null;
        return false;
    }

    private bool TryAcquireTarget(EntityUid uid, FSRevenantComponent comp, MapCoordinates myPos,
        out EntityUid target, out MapCoordinates targetPos)
    {
        target = default;
        targetPos = default;

        if (comp.MarkedTarget is { } marked && IsPursuable(comp, marked, myPos, out targetPos, ignoreRange: true))
        {
            target = marked;
            return true;
        }

        var bestDist = float.MaxValue;
        var query = EntityQueryEnumerator<ActorComponent>();
        while (query.MoveNext(out var candidate, out _))
        {
            if (!IsPursuable(comp, candidate, myPos, out var candidatePos))
                continue;

            var dist = Vector2.Distance(myPos.Position, candidatePos.Position);
            if (dist >= bestDist)
                continue;

            bestDist = dist;
            target = candidate;
            targetPos = candidatePos;
        }

        return bestDist < float.MaxValue;
    }

    private bool IsPursuable(FSRevenantComponent comp, EntityUid candidate, MapCoordinates myPos,
        out MapCoordinates candidatePos, bool ignoreRange = false)
    {
        candidatePos = default;

        if (TerminatingOrDeleted(candidate))
            return false;

        if (!IsValidVictim(candidate))
            return false;

        candidatePos = _transform.GetMapCoordinates(candidate);
        if (candidatePos.MapId != myPos.MapId)
            return false;

        return ignoreRange
            || Vector2.Distance(myPos.Position, candidatePos.Position) <= comp.TargetSearchRange;
    }

    public string GetTelemetry()
        => $"combos opened={_combosOpened} completed={_combosCompleted} | " +
           $"executes fired={_executesFired} refused={_executesRefused} | marks={_marksPlaced}";

    public void ResetTelemetry()
    {
        _combosOpened = _combosCompleted = _executesFired = _executesRefused = _marksPlaced = 0;
    }

    public bool IsValidVictim(EntityUid candidate)
    {
        if (TerminatingOrDeleted(candidate))
            return false;
        if (!HasComp<ActorComponent>(candidate))
            return false;
        if (!TryComp<MobStateComponent>(candidate, out var state) || state.CurrentState != MobState.Alive)
            return false;

        return !HasComp<WaveSpawnedTagComponent>(candidate) && !HasComp<GhostComponent>(candidate);
    }

    private bool ReadWaveState()
    {
        _cccEntity = null;

        var query = EntityQueryEnumerator<WaveGameRuleComponent>();
        while (query.MoveNext(out _, out var waveComp))
        {
            var ccc = waveComp.CCCEntity;
            if (ccc.IsValid())
                _cccEntity = ccc;
        }

        return _waveRule.IsDarkWaveActive();
    }

    private void BeginChannel(EntityUid uid)
    {
        EnsureComp<FSRevenantChannelComponent>(uid);
    }

    private void EndChannel(EntityUid uid, FSRevenantComponent comp)
    {
        comp.IsExecuting = false;
        comp.IsGrabPaused = false;

        if (comp.GrabTarget is { } grabbed && !TerminatingOrDeleted(grabbed))
            RemCompDeferred<FSRevenantGrabbedComponent>(grabbed);

        comp.GrabTarget = null;
        RemCompDeferred<FSRevenantChannelComponent>(uid);
    }
}
