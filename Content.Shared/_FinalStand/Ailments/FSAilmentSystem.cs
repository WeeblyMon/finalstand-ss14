using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.HealthExaminable;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._FinalStand.Ailments;

public sealed partial class FSAilmentSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    private static readonly ProtoId<FSAilmentRulesPrototype> RulesId = "Default";

    private EntityQuery<BloodstreamComponent> _bloodQuery;
    private EntityQuery<FSAilmentStateComponent> _stateQuery;
    private readonly List<(EntityUid Patient, EntProtoId Next)> _pendingExpiry = new();
    private readonly List<FSAilmentView> _examineViews = new();
    private int _internalDepth;
    private bool _fieldOps;
    private TimeSpan? _mendingDuration;
    private float _wardScale = 1f;

    public FSAilmentRulesPrototype Rules => _proto.Index(RulesId);

    public bool InInternalDamage => _internalDepth > 0;

    public static readonly FSAilmentSlot[] Slots = Enum.GetValues<FSAilmentSlot>();

    private const float BleedEpsilon = 0.001f;

    public override void Initialize()
    {
        base.Initialize();

        _bloodQuery = GetEntityQuery<BloodstreamComponent>();
        _stateQuery = GetEntityQuery<FSAilmentStateComponent>();

        SubscribeLocalEvent<FSAilmentStateComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FSAilmentStateComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<FSAilmentStateComponent, BleedModifierEvent>(OnBleedModifier);
        SubscribeLocalEvent<FSAilmentStateComponent, FSAilmentInflictAttemptEvent>(OnInflictAttempt);
        SubscribeLocalEvent<FSAilmentStateComponent, FSIncomingHealScaleEvent>(OnIncomingHealScale);
        SubscribeLocalEvent<FSAilmentStateComponent, HealthBeingExaminedEvent>(OnHealthExamined);
        SubscribeLocalEvent<FSSignatureAilmentComponent, ExaminedEvent>(OnSignatureExamined);
        SubscribeLocalEvent<FSAilmentStateComponent, ReactionEntityEvent>(OnReaction);
        SubscribeLocalEvent<FSAilmentComponent, StatusEffectAppliedEvent>(OnAilmentApplied);
        SubscribeLocalEvent<FSAilmentComponent, StatusEffectRemovedEvent>(OnAilmentRemoved);
        SubscribeLocalEvent<FSAilmentStateComponent, EntityUnpausedEvent>(OnUnpaused);
    }

    private void OnUnpaused(Entity<FSAilmentStateComponent> ent, ref EntityUnpausedEvent args)
    {
        if (ent.Comp.DownEnd is { } downEnd)
        {
            ent.Comp.DownEnd = downEnd + args.PausedTime;
            DirtyField(ent.AsNullable(), nameof(FSAilmentStateComponent.DownEnd));
        }

        // Zero is "never hit" and stays that way.
        if (ent.Comp.LastDamage != TimeSpan.Zero)
            ent.Comp.LastDamage += args.PausedTime;

        if (ent.Comp.RecoveringSince is { } since)
            ent.Comp.RecoveringSince = since + args.PausedTime;

        foreach (var meter in ent.Comp.Meters.Keys.ToArray())
        {
            var state = ent.Comp.Meters[meter];
            ent.Comp.Meters[meter] = state with { Updated = state.Updated + args.PausedTime };
        }

        var samples = ent.Comp.DownSamples.Count;
        for (var i = 0; i < samples; i++)
        {
            var (time, damage) = ent.Comp.DownSamples.Dequeue();
            ent.Comp.DownSamples.Enqueue((time + args.PausedTime, damage));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingExpiry.Count == 0)
            return;

        foreach (var (patient, next) in _pendingExpiry)
        {
            if (TerminatingOrDeleted(patient))
                continue;

            if (next != Rules.Mending)
                ApplyUnchecked(patient, next);
            else if (GetAilment(MendingEffect) is { } mending)
                _status.TrySetStatusEffectDuration(patient, MendingEffect, _mendingDuration ?? mending.Duration);
        }

        _pendingExpiry.Clear();
    }

    public InternalDamageScope Internal()
    {
        _internalDepth++;
        return new InternalDamageScope(this);
    }

    public void Inflict(EntityUid patient, FSInjurySpec injury, EntityUid? source = null)
    {
        if (_net.IsClient || !_stateQuery.HasComp(patient) || _mobState.IsDead(patient))
            return;

        if (injury.Bleed > 0)
            RaiseBleeding(patient, injury.Bleed, source);

        if (injury.Fracture)
            Fracture(patient, source);

        if (injury.Burn)
            Apply(patient, Rules.Burned, source);

        if (injury.Poison)
            Apply(patient, Rules.Poisoned, source);

        foreach (var (meter, amount) in injury.Meters)
        {
            AddMeter(patient, meter, amount, source);
        }
    }

    public bool Apply(EntityUid patient, EntProtoId effect, EntityUid? source = null)
    {
        if (_net.IsClient || !_stateQuery.HasComp(patient) || _mobState.IsDead(patient) || GetAilment(effect) is not { } ailment)
            return false;

        if (!TryAttempt(patient, ailment.Slot))
            return false;

        var fresh = !HasAilment(patient, effect);
        if (!ApplyUnchecked(patient, effect))
            return false;

        if (fresh)
            Inflicted(patient, ailment.Slot, source, effect);

        return true;
    }

    public void InflictHit(EntityUid patient, FSInjurySpec injury, float dealt, EntityUid? source = null)
    {
        if (injury.IgnoreFloor || dealt >= Rules.InjuryFloor)
            Inflict(patient, injury, source);
    }

    public void AddMeter(EntityUid patient, FSAilmentMeter meter, float amount, EntityUid? source = null, bool draining = true)
    {
        if (_net.IsClient || amount <= 0f || !_stateQuery.TryComp(patient, out var state) || _mobState.IsDead(patient))
            return;

        if (!Rules.Meters.TryGetValue(meter, out var rule) || !TryAttempt(patient, rule.Slot))
            return;

        var value = MeterValue(state, meter, rule) + amount;
        var stored = value >= 100f ? 0f : value;
        state.Meters[meter] = new FSAilmentMeterState(stored, _timing.CurTime, draining);
        SetWarning((patient, state), rule.Slot, stored);

        if (value >= 100f)
            Inflict(patient, rule.Injury, source);
    }

    public void DrainMeter(EntityUid patient, FSAilmentMeter meter)
    {
        if (!_stateQuery.TryComp(patient, out var state) || !state.Meters.TryGetValue(meter, out var current) || current.Draining)
            return;

        state.Meters[meter] = current with { Updated = _timing.CurTime, Draining = true };
    }

    public void RefreshWarnings(Entity<FSAilmentStateComponent> patient)
    {
        foreach (var (meter, rule) in Rules.Meters)
        {
            SetWarning(patient, rule.Slot, MeterValue(patient.Comp, meter, rule));
        }
    }

    public bool Cure(EntityUid patient, FSAilmentSlot slot, EntityUid? curer = null, FSCureKind kind = FSCureKind.HandsOn)
    {
        if (slot == FSAilmentSlot.Bleeding)
            return StopBleeding(patient, curer, kind);

        var cured = false;
        var tier = FSAilmentTier.None;
        foreach (var effect in Active<FSAilmentComponent>(patient).ToList())
        {
            if (effect.Comp2.Slot != slot)
                continue;

            if (effect.Comp2.Tier > tier)
                tier = effect.Comp2.Tier;

            RemoveEffect(effect);
            cured = true;
        }

        if (cured)
            RaiseCured(patient, slot, tier, curer, kind);

        return cured;
    }

    public bool StopBleeding(EntityUid patient, EntityUid? curer = null, FSCureKind kind = FSCureKind.HandsOn)
    {
        if (!_bloodQuery.TryComp(patient, out var blood) || blood.BleedAmount <= 0f)
            return false;

        var stopped = blood.BleedAmount;
        _bloodstream.TryModifyBleedAmount((patient, blood), -blood.BleedAmount);
        ReportBleedStopped(patient, stopped, curer, kind);
        return true;
    }

    public void ReportBleedStopped(EntityUid patient, float stopped, EntityUid? curer = null, FSCureKind kind = FSCureKind.HandsOn)
    {
        RaiseCured(patient, FSAilmentSlot.Bleeding, TakeBleedTier(patient, stopped), curer, kind);
    }

    public void SetWardScale(float scale)
    {
        _wardScale = scale;
    }

    public bool ApplyWard(EntityUid patient, EntProtoId ward)
    {
        if (_net.IsClient || !_stateQuery.HasComp(patient) || _mobState.IsDead(patient))
            return false;

        if (!_proto.TryIndex(ward, out var proto) || !proto.TryGetComponent<FSAilmentWardComponent>(out var wardComp, Factory))
            return false;

        if (_status.HasStatusEffect(patient, ward))
            return false;

        return _status.TrySetStatusEffectDuration(patient, ward, wardComp.Duration * _wardScale);
    }

    public bool IsBroken(EntityUid patient)
    {
        return HasAilment(patient, Rules.Broken);
    }

    public bool AddMend(EntityUid patient, FSAilmentSlot slot, float fraction, EntityUid? curer = null, FSCureKind kind = FSCureKind.Automatic)
    {
        if (_net.IsClient || fraction <= 0f || !_stateQuery.TryComp(patient, out var state) || !Has(patient, slot))
            return false;

        var progress = state.MendProgress.GetValueOrDefault(slot) + fraction;
        if (progress < 1f)
        {
            state.MendProgress[slot] = progress;
            SetMendFill((patient, state), slot, progress);
            return false;
        }

        return Cure(patient, slot, curer, kind);
    }

    public void PruneMend(Entity<FSAilmentStateComponent> patient)
    {
        if (patient.Comp.MendProgress.Count == 0)
            return;

        foreach (var slot in Slots)
        {
            if (patient.Comp.MendProgress.ContainsKey(slot) && !Has(patient, slot))
                ClearMend(patient, slot);
        }
    }

    private EntProtoId MendingEffect => _fieldOps ? Rules.MendingFieldOps : Rules.Mending;

    public void SetMendingDuration(TimeSpan? duration)
    {
        _mendingDuration = duration;
    }

    public void SetFieldOps(bool active)
    {
        if (_fieldOps == active)
            return;

        _fieldOps = active;
        var from = active ? Rules.Mending : Rules.MendingFieldOps;
        var to = MendingEffect;
        var now = _timing.CurTime;

        var swaps = new List<(EntityUid Patient, EntityUid Effect, TimeSpan? Left)>();
        var query = EntityQueryEnumerator<FSAilmentStateComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            foreach (var effect in Active<FSAilmentComponent>(uid))
            {
                if (Prototype(effect)?.ID != from.Id)
                    continue;

                swaps.Add((uid, effect, effect.Comp1.EndEffectTime - now));
                break;
            }
        }

        foreach (var (patient, effect, left) in swaps)
        {
            RemoveEffect(effect);
            _status.TrySetStatusEffectDuration(patient, to, left);
        }
    }

    public bool Clot(EntityUid patient, float amount, EntityUid? curer = null, FSCureKind kind = FSCureKind.Automatic)
    {
        if (amount <= 0f || !_bloodQuery.TryComp(patient, out var blood) || blood.BleedAmount <= 0f)
            return false;

        if (blood.BleedAmount > amount)
        {
            _bloodstream.TryModifyBleedAmount((patient, blood), -amount);
            return true;
        }

        return StopBleeding(patient, curer, kind);
    }

    public void ClearAll(EntityUid patient)
    {
        if (!_stateQuery.TryComp(patient, out var state))
            return;

        if (_bloodQuery.TryComp(patient, out var blood) && blood.BleedAmount > 0f)
            _bloodstream.TryModifyBleedAmount((patient, blood), -blood.BleedAmount);

        RemoveAll(patient);
        ResetState((patient, state));
    }

    public int GetBleedLevel(EntityUid patient)
    {
        return (int) MathF.Ceiling(GetBleedAmount(patient) - BleedEpsilon);
    }

    public float GetBleedAmount(EntityUid patient)
    {
        return _bloodQuery.TryComp(patient, out var blood) ? blood.BleedAmount : 0f;
    }

    public float GetClotStep(EntityUid patient)
    {
        return _bloodQuery.TryComp(patient, out var blood) ? blood.BleedReductionAmount : 0f;
    }

    public bool Has(EntityUid patient, FSAilmentSlot slot)
    {
        if (slot == FSAilmentSlot.Bleeding)
            return GetBleedLevel(patient) > 0;

        foreach (var effect in Active<FSAilmentComponent>(patient))
        {
            if (effect.Comp2.Slot == slot)
                return true;
        }

        return false;
    }

    public void GetViews(EntityUid patient, List<FSAilmentView> views)
    {
        views.Clear();

        var bleed = GetBleedLevel(patient);
        if (bleed > 0 && Rules.Slots.TryGetValue(FSAilmentSlot.Bleeding, out var bleeding))
            views.Add(new FSAilmentView(FSAilmentSlot.Bleeding, bleeding.Label, bleed, null, bleeding.Effect, bleeding.FixHint, null, false));

        foreach (var effect in Active<FSAilmentComponent>(patient))
        {
            var ailment = effect.Comp2;
            views.Add(new FSAilmentView(ailment.Slot, ailment.Name, 0, effect.Comp1.EndEffectTime, ailment.Effect, ailment.FixHint, ailment.Examine, false, ailment.Suffix, ailment.FixIcon));
        }

        foreach (var ward in Active<FSAilmentWardComponent>(patient))
        {
            views.Add(new FSAilmentView(ward.Comp2.Blocks, ward.Comp2.Name, 0, ward.Comp1.EndEffectTime, ward.Comp2.Effect, null, null, true));
        }
    }

    public void GetOverlays(EntityUid patient, Dictionary<FSAilmentSlot, SpriteSpecifier> overlays)
    {
        overlays.Clear();
        foreach (var effect in Active<FSAilmentComponent>(patient))
        {
            if (effect.Comp2.Overlay is { } overlay)
                overlays[effect.Comp2.Slot] = overlay;
        }
    }

    public FSAilmentPips GetPips(EntityUid patient)
    {
        var pips = GetBleedLevel(patient) > 0 ? FSAilmentPips.Bleeding : FSAilmentPips.None;
        foreach (var effect in Active<FSAilmentComponent>(patient))
        {
            pips |= (FSAilmentPips) (1 << (int) effect.Comp2.Slot);
        }

        return pips;
    }

    public bool BlocksRegen(EntityUid patient)
    {
        return RegenBlocker(patient) != null;
    }

    public LocId? RegenBlocker(EntityUid patient)
    {
        if (GetBleedLevel(patient) > 0)
            return Rules.Slots[FSAilmentSlot.Bleeding].Label;

        foreach (var effect in Active<FSAilmentComponent>(patient))
        {
            if (effect.Comp2.BlocksRegen)
                return effect.Comp2.Name;
        }

        return null;
    }

    public DamageSpecifier? GetTickDamage(EntityUid patient)
    {
        DamageSpecifier? total = null;
        foreach (var effect in Active<FSAilmentComponent>(patient))
        {
            if (effect.Comp2.TickDamage is not { } tick)
                continue;

            total = total == null ? new DamageSpecifier(tick) : total + tick;
        }

        return total;
    }

    public float GetIncomingHealScale(EntityUid patient)
    {
        var ev = new FSIncomingHealScaleEvent(1f);
        RaiseLocalEvent(patient, ref ev);
        return ev.Scale;
    }

    public void SetClock(Entity<FSAilmentStateComponent> patient, TimeSpan? end, bool held)
    {
        if (patient.Comp.DownEnd != end)
        {
            patient.Comp.DownEnd = end;
            DirtyField(patient.AsNullable(), nameof(FSAilmentStateComponent.DownEnd));
        }

        if (patient.Comp.Held != held)
        {
            patient.Comp.Held = held;
            DirtyField(patient.AsNullable(), nameof(FSAilmentStateComponent.Held));
        }
    }

    public void ClearClock(Entity<FSAilmentStateComponent> patient)
    {
        patient.Comp.DownSamples.Clear();
        SetClock(patient, null, false);
    }

    public void SetRecovering(Entity<FSAilmentStateComponent> patient, TimeSpan? since)
    {
        patient.Comp.RecoveringSince = since;
        var recovering = since != null;
        if (patient.Comp.Recovering == recovering)
            return;

        patient.Comp.Recovering = recovering;
        DirtyField(patient.AsNullable(), nameof(FSAilmentStateComponent.Recovering));
    }

    public void SetRegenerating(Entity<FSAilmentStateComponent> patient, bool regenerating)
    {
        if (patient.Comp.Regenerating == regenerating)
            return;

        patient.Comp.Regenerating = regenerating;
        DirtyField(patient.AsNullable(), nameof(FSAilmentStateComponent.Regenerating));
    }

    public void EndBleed(EntityUid patient)
    {
        if (_stateQuery.TryComp(patient, out var state))
            state.PeakBleed = 0;
    }

    private FSAilmentTier TakeBleedTier(EntityUid patient, float bleedAmount)
    {
        var peak = (int) MathF.Ceiling(bleedAmount);
        if (_stateQuery.TryComp(patient, out var state))
        {
            peak = Math.Max(peak, state.PeakBleed);
            state.PeakBleed = 0;
        }

        return peak >= Rules.MajorBleedLevel ? FSAilmentTier.Major : FSAilmentTier.Minor;
    }

    private void RaiseBleeding(EntityUid patient, int level, EntityUid? source)
    {
        if (!_bloodQuery.TryComp(patient, out var blood) || blood.BleedAmount >= level)
            return;

        if (!TryAttempt(patient, FSAilmentSlot.Bleeding))
            return;

        _bloodstream.TryModifyBleedAmount((patient, blood), level - blood.BleedAmount);
        if (_stateQuery.TryComp(patient, out var state))
            state.PeakBleed = Math.Max(state.PeakBleed, level);

        Inflicted(patient, FSAilmentSlot.Bleeding, source);
    }

    private void Fracture(EntityUid patient, EntityUid? source)
    {
        var broken = Rules.Broken;
        if (HasAilment(patient, broken) || !TryAttempt(patient, FSAilmentSlot.Bone))
            return;

        RemoveSlot(patient, FSAilmentSlot.Bone);
        if (ApplyUnchecked(patient, broken))
            Inflicted(patient, FSAilmentSlot.Bone, source, broken);
    }

    private bool ApplyUnchecked(EntityUid patient, EntProtoId effect)
    {
        return GetAilment(effect) is { } ailment && _status.TrySetStatusEffectDuration(patient, effect, ailment.Duration);
    }

    private void RemoveSlot(EntityUid patient, FSAilmentSlot slot)
    {
        foreach (var effect in Active<FSAilmentComponent>(patient))
        {
            if (effect.Comp2.Slot == slot)
            {
                RemoveEffect(effect);
                return;
            }
        }
    }

    private void RemoveAll(EntityUid patient)
    {
        var effects = new List<EntityUid>();
        foreach (var effect in Active<FSAilmentComponent>(patient))
        {
            effects.Add(effect);
        }

        foreach (var effect in effects)
        {
            RemoveEffect(effect);
        }
    }

    private void RemoveEffect(EntityUid effect)
    {
        if (!_net.IsClient && _container.TryGetContainingContainer((effect, null, null), out var container))
            _container.Remove(effect, container);

        PredictedQueueDel(effect);
    }

    private IEnumerable<Entity<StatusEffectComponent, T>> Active<T>(EntityUid patient) where T : Component
    {
        foreach (var effect in _status.EnumerateStatusEffects<T>(patient))
        {
            if (!EntityManager.IsQueuedForDeletion(effect) && !TerminatingOrDeleted(effect))
                yield return effect;
        }
    }

    public bool HasAilment(EntityUid patient, EntProtoId effect)
    {
        foreach (var active in Active<FSAilmentComponent>(patient))
        {
            if (Prototype(active)?.ID == effect.Id)
                return true;
        }

        return false;
    }

    private bool TryAttempt(EntityUid patient, FSAilmentSlot slot)
    {
        var ev = new FSAilmentInflictAttemptEvent(slot);
        RaiseLocalEvent(patient, ref ev);
        return !ev.Cancelled;
    }

    private void Inflicted(EntityUid patient, FSAilmentSlot slot, EntityUid? source, EntProtoId? effect = null)
    {
        var ev = new FSAilmentInflictedEvent(patient, slot, source, effect);
        RaiseLocalEvent(patient, ref ev, true);
    }

    private void RaiseCured(EntityUid patient, FSAilmentSlot slot, FSAilmentTier tier, EntityUid? curer, FSCureKind kind)
    {
        if (_stateQuery.TryComp(patient, out var state))
            ClearMend((patient, state), slot);

        var ev = new FSAilmentCuredEvent(patient, slot, tier, curer, kind);
        RaiseLocalEvent(patient, ref ev, true);
    }

    public FSAilmentComponent? GetAilment(EntProtoId effect)
    {
        return _proto.TryIndex(effect, out var proto) && proto.TryGetComponent<FSAilmentComponent>(out var ailment, Factory)
            ? ailment
            : null;
    }

    private float MeterValue(FSAilmentStateComponent state, FSAilmentMeter meter, FSAilmentMeterRule rule)
    {
        if (!state.Meters.TryGetValue(meter, out var current))
            return 0f;

        if (!current.Draining)
            return current.Value;

        var elapsed = (float) (_timing.CurTime - current.Updated).TotalSeconds;
        return MathF.Max(0f, current.Value - rule.DrainPerSecond * elapsed);
    }

    private void SetWarning(Entity<FSAilmentStateComponent> patient, FSAilmentSlot slot, float value)
    {
        var rules = Rules;
        byte level = value >= rules.WarningHigh ? (byte) 2 : value >= rules.WarningLow ? (byte) 1 : (byte) 0;
        var current = patient.Comp.Warnings.GetValueOrDefault(slot);
        if (current == level)
            return;

        if (level == 0)
            patient.Comp.Warnings.Remove(slot);
        else
            patient.Comp.Warnings[slot] = level;

        DirtyField(patient.AsNullable(), nameof(FSAilmentStateComponent.Warnings));
    }

    private void SetMendFill(Entity<FSAilmentStateComponent> patient, FSAilmentSlot slot, float progress)
    {
        var fill = (byte) Math.Clamp((int) (progress * 10f), 0, 10);
        if (fill == patient.Comp.Mend.GetValueOrDefault(slot))
            return;

        if (fill == 0)
            patient.Comp.Mend.Remove(slot);
        else
            patient.Comp.Mend[slot] = fill;

        DirtyField(patient.AsNullable(), nameof(FSAilmentStateComponent.Mend));
    }

    private void ClearMend(Entity<FSAilmentStateComponent> patient, FSAilmentSlot slot)
    {
        patient.Comp.MendProgress.Remove(slot);
        SetMendFill(patient, slot, 0f);
    }

    private void ResetState(Entity<FSAilmentStateComponent> patient)
    {
        patient.Comp.Meters.Clear();
        patient.Comp.MendProgress.Clear();
        patient.Comp.PeakBleed = 0;
        if (patient.Comp.Mend.Count > 0)
        {
            patient.Comp.Mend.Clear();
            DirtyField(patient.AsNullable(), nameof(FSAilmentStateComponent.Mend));
        }

        if (patient.Comp.Warnings.Count == 0)
            return;

        patient.Comp.Warnings.Clear();
        DirtyField(patient.AsNullable(), nameof(FSAilmentStateComponent.Warnings));
    }

    private void OnMobStateChanged(Entity<FSAilmentStateComponent> ent, ref MobStateChangedEvent args)
    {
        if (!_net.IsClient)
        {
            if (args.NewMobState == MobState.Critical)
                ent.Comp.DownSamples.Clear();
            else
                ClearClock(ent);

            if (args.NewMobState != MobState.Alive)
                SetRegenerating(ent, false);
        }

        if (args.NewMobState != MobState.Dead)
            return;

        ent.Comp.PeakBleed = 0;
        if (_bloodQuery.TryComp(ent, out var blood) && blood.BleedAmount > 0f)
            _bloodstream.TryModifyBleedAmount((ent.Owner, blood), -blood.BleedAmount);
    }

    private void OnRejuvenate(Entity<FSAilmentStateComponent> ent, ref RejuvenateEvent args)
    {
        ResetState(ent);
    }

    private void OnBleedModifier(Entity<FSAilmentStateComponent> ent, ref BleedModifierEvent args)
    {
        if (_mobState.IsCritical(ent))
            args.BleedReductionAmount = 0f;
    }

    private void OnInflictAttempt(Entity<FSAilmentStateComponent> ent, ref FSAilmentInflictAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        foreach (var ward in Active<FSAilmentWardComponent>(ent.Owner))
        {
            if (ward.Comp2.Blocks != args.Slot)
                continue;

            args.Cancelled = true;
            return;
        }
    }

    private void OnIncomingHealScale(Entity<FSAilmentStateComponent> ent, ref FSIncomingHealScaleEvent args)
    {
        foreach (var effect in Active<FSAilmentComponent>(ent.Owner))
        {
            args.Scale *= effect.Comp2.IncomingHealScale;
        }
    }

    private void OnHealthExamined(Entity<FSAilmentStateComponent> ent, ref HealthBeingExaminedEvent args)
    {
        GetViews(ent, _examineViews);
        foreach (var view in _examineViews)
        {
            if (view.Ward || view.Examine is not { } examine)
                continue;

            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString(examine, ("target", ent.Owner)));
        }
    }

    private void OnReaction(Entity<FSAilmentStateComponent> ent, ref ReactionEntityEvent args)
    {
        var rules = Rules;
        if (_net.IsClient
            || args.Method != ReactionMethod.Touch
            || args.ReagentQuantity.Quantity < rules.WashMinimum
            || !rules.WashReagents.Contains(args.Reagent.ID))
        {
            return;
        }

        Cure(ent, rules.WashSlot, kind: FSCureKind.Wash);
    }

    private void OnSignatureExamined(Entity<FSSignatureAilmentComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Examine));
    }

    private void OnAilmentApplied(Entity<FSAilmentComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (HasComp<MovementModStatusEffectComponent>(ent))
            _movementSpeed.RefreshMovementSpeedModifiers(args.Target);
    }

    private void OnAilmentRemoved(Entity<FSAilmentComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (_net.IsClient || ent.Comp.BecomesOnExpire is not { } next)
            return;

        if (!TryComp<StatusEffectComponent>(ent, out var status) || status.EndEffectTime is not { } end || end > _timing.CurTime)
            return;

        _pendingExpiry.Add((args.Target, next));
    }

    public readonly struct InternalDamageScope(FSAilmentSystem system) : IDisposable
    {
        public void Dispose()
        {
            system._internalDepth--;
        }
    }
}
