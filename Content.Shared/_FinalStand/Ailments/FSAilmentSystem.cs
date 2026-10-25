using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Ailments;

public sealed partial class FSAilmentSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    private static readonly ProtoId<FSAilmentRulesPrototype> RulesId = "Default";

    private EntityQuery<BloodstreamComponent> _bloodQuery;
    private EntityQuery<FSAilmentStateComponent> _stateQuery;
    private readonly List<(EntityUid Patient, EntProtoId Next)> _pendingExpiry = new();
    private int _internalDepth;

    public FSAilmentRulesPrototype Rules => _proto.Index(RulesId);

    public bool InInternalDamage => _internalDepth > 0;

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
        SubscribeLocalEvent<FSAilmentComponent, StatusEffectRemovedEvent>(OnAilmentRemoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingExpiry.Count == 0)
            return;

        foreach (var (patient, next) in _pendingExpiry)
        {
            if (!TerminatingOrDeleted(patient))
                ApplyUnchecked(patient, next);
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

        ApplyUnchecked(patient, effect);
        Inflicted(patient, ailment.Slot, source);
        return true;
    }

    public void AddMeter(EntityUid patient, FSAilmentMeter meter, float amount, EntityUid? source = null)
    {
        if (_net.IsClient || amount <= 0f || !_stateQuery.TryComp(patient, out var state) || _mobState.IsDead(patient))
            return;

        if (!Rules.Meters.TryGetValue(meter, out var rule))
            return;

        var value = MeterValue(state, meter, rule) + amount;
        state.Meters[meter] = new FSAilmentMeterState(value >= 100f ? 0f : value, _timing.CurTime);
        SetWarning((patient, state), rule.Slot, value >= 100f ? 0f : value);

        if (value >= 100f)
            Inflict(patient, rule.Injury, source);
    }

    public void RefreshWarnings(Entity<FSAilmentStateComponent> patient)
    {
        foreach (var (meter, rule) in Rules.Meters)
        {
            SetWarning(patient, rule.Slot, MeterValue(patient.Comp, meter, rule));
        }
    }

    public bool Cure(EntityUid patient, FSAilmentSlot slot, EntityUid? curer = null, bool automatic = false)
    {
        if (slot == FSAilmentSlot.Bleeding)
            return StopBleeding(patient, curer, automatic);

        foreach (var effect in Active<FSAilmentComponent>(patient))
        {
            if (effect.Comp2.Slot != slot || Prototype(effect)?.ID is not { } id)
                continue;

            var tier = effect.Comp2.Tier;
            _status.TryRemoveStatusEffect(patient, id);
            RaiseCured(patient, slot, tier, curer, automatic);
            return true;
        }

        return false;
    }

    public bool StopBleeding(EntityUid patient, EntityUid? curer = null, bool automatic = false)
    {
        if (!_bloodQuery.TryComp(patient, out var blood) || blood.BleedAmount <= 0f)
            return false;

        var tier = TakeBleedTier(patient, blood.BleedAmount);
        _bloodstream.TryModifyBleedAmount((patient, blood), -blood.BleedAmount);
        RaiseCured(patient, FSAilmentSlot.Bleeding, tier, curer, automatic);
        return true;
    }

    public bool Clot(EntityUid patient, float amount, EntityUid? curer = null, bool automatic = false)
    {
        if (amount <= 0f || !_bloodQuery.TryComp(patient, out var blood) || blood.BleedAmount <= 0f)
            return false;

        if (blood.BleedAmount > amount)
        {
            _bloodstream.TryModifyBleedAmount((patient, blood), -amount);
            return true;
        }

        return StopBleeding(patient, curer, automatic);
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
        return _bloodQuery.TryComp(patient, out var blood) ? (int) MathF.Ceiling(blood.BleedAmount) : 0;
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

    public bool BlocksRegen(EntityUid patient)
    {
        if (GetBleedLevel(patient) > 0)
            return true;

        foreach (var effect in Active<FSAilmentComponent>(patient))
        {
            if (effect.Comp2.BlocksRegen)
                return true;
        }

        return false;
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
        if (HasEffect(patient, broken) || !TryAttempt(patient, FSAilmentSlot.Bone))
            return;

        RemoveSlot(patient, FSAilmentSlot.Bone);
        ApplyUnchecked(patient, broken);
        Inflicted(patient, FSAilmentSlot.Bone, source);
    }

    private void ApplyUnchecked(EntityUid patient, EntProtoId effect)
    {
        if (GetAilment(effect) is { } ailment)
            _status.TrySetStatusEffectDuration(patient, effect, ailment.Duration);
    }

    private void RemoveSlot(EntityUid patient, FSAilmentSlot slot)
    {
        foreach (var effect in Active<FSAilmentComponent>(patient))
        {
            if (effect.Comp2.Slot == slot && Prototype(effect)?.ID is { } id)
            {
                _status.TryRemoveStatusEffect(patient, id);
                return;
            }
        }
    }

    private void RemoveAll(EntityUid patient)
    {
        var ids = new List<EntProtoId>();
        foreach (var effect in Active<FSAilmentComponent>(patient))
        {
            if (Prototype(effect)?.ID is { } id)
                ids.Add(id);
        }

        foreach (var id in ids)
        {
            _status.TryRemoveStatusEffect(patient, id);
        }
    }

    private IEnumerable<Entity<StatusEffectComponent, T>> Active<T>(EntityUid patient) where T : Component
    {
        foreach (var effect in _status.EnumerateStatusEffects<T>(patient))
        {
            if (!EntityManager.IsQueuedForDeletion(effect) && !TerminatingOrDeleted(effect))
                yield return effect;
        }
    }

    private bool HasEffect(EntityUid patient, EntProtoId effect)
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

    private void Inflicted(EntityUid patient, FSAilmentSlot slot, EntityUid? source)
    {
        if (_stateQuery.TryComp(patient, out var state) && source is { } cause && !TerminatingOrDeleted(cause))
            state.Causes[slot] = Name(cause);

        var ev = new FSAilmentInflictedEvent(patient, slot, source);
        RaiseLocalEvent(patient, ref ev, true);
    }

    private void RaiseCured(EntityUid patient, FSAilmentSlot slot, FSAilmentTier tier, EntityUid? curer, bool automatic)
    {
        var ev = new FSAilmentCuredEvent(patient, slot, tier, curer, automatic);
        RaiseLocalEvent(patient, ref ev, true);
    }

    private FSAilmentComponent? GetAilment(EntProtoId effect)
    {
        return _proto.TryIndex(effect, out var proto) && proto.TryGetComponent<FSAilmentComponent>(out var ailment, Factory)
            ? ailment
            : null;
    }

    private float MeterValue(FSAilmentStateComponent state, FSAilmentMeter meter, FSAilmentMeterRule rule)
    {
        if (!state.Meters.TryGetValue(meter, out var current))
            return 0f;

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

    private void ResetState(Entity<FSAilmentStateComponent> patient)
    {
        patient.Comp.Meters.Clear();
        patient.Comp.Causes.Clear();
        patient.Comp.PeakBleed = 0;
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
