using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.Ailments;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Ailments;

public sealed partial class FSVitalsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly List<Entity<FSAilmentStateComponent, DamageableComponent>> _patients = new();
    private readonly List<(ProtoId<DamageTypePrototype> Type, FixedPoint2 Amount)> _types = new();
    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
    }

    private void OnPrepStarted(ref WavePrepStartedEvent ev)
    {
        var query = EntityQueryEnumerator<FSAilmentStateComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _ailments.ClearAll(uid);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        _nextTick = now + TickInterval;

        _patients.Clear();
        var query = EntityQueryEnumerator<FSAilmentStateComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var state, out var damageable))
        {
            _patients.Add((uid, state, damageable));
        }

        var rules = _ailments.Rules;
        foreach (var patient in _patients)
        {
            if (!TerminatingOrDeleted(patient))
                Tick(patient, rules, now);
        }
    }

    private void Tick(Entity<FSAilmentStateComponent, DamageableComponent> patient, FSAilmentRulesPrototype rules, TimeSpan now)
    {
        if (_mobState.IsDead(patient))
        {
            _ailments.ClearClock(patient);
            _ailments.SetRegenerating(patient, false);
            return;
        }

        TrackBleedEnd(patient);
        var bleed = _ailments.GetBleedLevel(patient);

        var critical = _mobState.IsCritical(patient);
        using (_ailments.Internal())
        {
            if (bleed > 0)
                Hurt(patient, rules.BleedDamage * bleed);

            if (critical)
                Hurt(patient, rules.DownDrain);

            if (_ailments.GetTickDamage(patient) is { } tick)
                Hurt(patient, tick);
        }

        if (_mobState.IsCritical(patient))
            UpdateClock(patient, rules, now, bleed);
        else
            _ailments.ClearClock(patient);

        Regenerate(patient, rules, now);
        _ailments.RefreshWarnings(patient);
        _ailments.PruneMend(patient);
    }

    private void TrackBleedEnd(Entity<FSAilmentStateComponent, DamageableComponent> patient)
    {
        var state = patient.Comp1;
        var amount = _ailments.GetBleedAmount(patient);
        if (amount <= 0f && state.PeakBleed > 0)
        {
            if (state.LastBleedAmount > _ailments.GetClotStep(patient) + 0.01f)
                _ailments.ReportBleedStopped(patient, state.LastBleedAmount, kind: FSCureKind.Chemical);
            else
                _ailments.EndBleed(patient);
        }

        state.LastBleedAmount = amount;
    }

    private void Hurt(Entity<FSAilmentStateComponent, DamageableComponent> patient, DamageSpecifier damage)
    {
        _damageable.TryChangeDamage((patient.Owner, patient.Comp2),
            damage,
            ignoreResistances: true,
            interruptsDoAfters: false,
            ignoreGlobalModifiers: true);
    }

    private void UpdateClock(Entity<FSAilmentStateComponent, DamageableComponent> patient, FSAilmentRulesPrototype rules, TimeSpan now, int bleed)
    {
        if (!_thresholds.TryGetThresholdForState(patient, MobState.Dead, out var dead))
            return;

        var state = patient.Comp1;
        var damage = patient.Comp2.TotalDamage.Float();
        var samples = state.DownSamples;
        samples.Enqueue((now, damage));
        while (samples.Count > 1 && now - samples.Peek().Time > rules.ClockWindow)
        {
            samples.Dequeue();
        }

        var oldest = samples.Peek();
        var span = (float) (now - oldest.Time).TotalSeconds;
        var rate = span > 0f
            ? (damage - oldest.Damage) / span
            : (rules.DownDrain.GetTotal() + rules.BleedDamage.GetTotal() * bleed).Float();

        if (rate <= 0f)
        {
            _ailments.SetClock(patient, null, true);
            return;
        }

        var end = now + TimeSpan.FromSeconds(MathF.Max(0f, dead.Value.Float() - damage) / rate);
        if (state.Held || state.DownEnd is not { } sent || (end - sent).Duration() >= rules.ClockResendDrift)
            _ailments.SetClock(patient, end, false);
    }

    private void Regenerate(Entity<FSAilmentStateComponent, DamageableComponent> patient, FSAilmentRulesPrototype rules, TimeSpan now)
    {
        var regenerating = !patient.Comp1.Recovering
            && _mobState.IsAlive(patient)
            && now - patient.Comp1.LastDamage >= rules.RegenDelay
            && !_ailments.BlocksRegen(patient)
            && Regen(patient, rules.RegenPerSecond * (float) TickInterval.TotalSeconds, rules.RegenCap);

        _ailments.SetRegenerating(patient, regenerating);
    }

    public bool Regen(Entity<FSAilmentStateComponent, DamageableComponent> patient, float amount, float cap)
    {
        if (!_thresholds.TryGetThresholdForState(patient, MobState.Critical, out var crit))
            return false;

        var excess = patient.Comp2.TotalDamage.Float() - crit.Value.Float() * (1f - cap);
        if (excess <= 0f)
            return false;

        _damageable.TryChangeDamage((patient.Owner, patient.Comp2),
            LargestFirst(patient.Comp2, FixedPoint2.New(MathF.Min(amount, excess))),
            ignoreResistances: true,
            interruptsDoAfters: false,
            origin: patient.Owner);

        return true;
    }

    private DamageSpecifier LargestFirst(DamageableComponent damageable, FixedPoint2 amount)
    {
        _types.Clear();
        foreach (var (type, value) in damageable.Damage.DamageDict)
        {
            if (value > 0)
                _types.Add((type, value));
        }

        _types.Sort((a, b) => b.Amount.CompareTo(a.Amount));

        var heal = new DamageSpecifier();
        foreach (var (type, value) in _types)
        {
            if (amount <= 0)
                break;

            var take = FixedPoint2.Min(amount, value);
            heal.DamageDict[type] = -take;
            amount -= take;
        }

        return heal;
    }
}
