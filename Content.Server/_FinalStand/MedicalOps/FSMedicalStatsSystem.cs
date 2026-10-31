using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Perks;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Ailments;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSMedicalStatsSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSMedicalFundSystem _medFund = default!;
    [Dependency] private FSTreatmentAttributionSystem _attribution = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private FSMedicalRosterSystem _roster = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private FSCombatMedicSystem _combatMedic = default!;
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private FSCasualtySystem _casualty = default!;

    private static readonly TimeSpan MinSurvivalForSave = TimeSpan.FromSeconds(60);

    public readonly record struct FSMedicalRoundStats(
        int HealingPoints, float HpHealed, int Stabilises, int Revives, int PatientsSaved, int AilmentsTreated);

    private readonly Dictionary<EntityUid, FSMedicalRoundStats> _roundStats = new();

    private readonly Dictionary<(EntityUid Medic, EntityUid Patient), (int Wave, int Count)> _topicalsPaid = new();
    private readonly Dictionary<(EntityUid Medic, EntityUid Patient), (int Wave, int Count)> _curesPaid = new();
    private readonly Dictionary<(EntityUid Medic, EntityUid Patient), FSMedicalLine> _linesThisTick = new();
    private readonly Dictionary<EntityUid, (int Count, TimeSpan Until)> _massPaid = new();
    private readonly Dictionary<(string Job, string Source), (int Credits, int Fund)> _waveIncome = new();
    private GameTick _payTick;

    public FSMedicalRoundStats GetStats(EntityUid mindId) => _roundStats.GetValueOrDefault(mindId);

    public IReadOnlyDictionary<EntityUid, FSMedicalRoundStats> GetRoundStats() => _roundStats;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSMedicalPatientComponent, DamageChangedEvent>(OnPatientDamageChanged);
        SubscribeLocalEvent<FSMedicalPatientComponent, MobStateChangedEvent>(OnPatientMobStateChanged);
        SubscribeLocalEvent<FSMedicalPatientComponent, FSTopicalAppliedEvent>(OnTopicalApplied);
        SubscribeLocalEvent<FSAilmentInflictedEvent>(OnAilmentInflicted);
        SubscribeLocalEvent<FSAilmentCuredEvent>(OnAilmentCured);

        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd, after: [typeof(FSLevelingSystem)]);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev) => EnsureComp<FSMedicalPatientComponent>(ev.Mob);

    private void OnPlayerAttached(PlayerAttachedEvent ev) => EnsureComp<FSMedicalPatientComponent>(ev.Entity);

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _roundStats.Clear();
        _topicalsPaid.Clear();
        _curesPaid.Clear();
        _linesThisTick.Clear();
        _massPaid.Clear();
        _waveIncome.Clear();
    }

    private void OnPrepStarted(WavePrepStartedEvent ev)
    {
        var query = EntityQueryEnumerator<FSMedicalPatientComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            comp.HealedSinceReset.Clear();
            comp.EnemyCaused.Clear();
        }
    }

    public void ResetPatient(EntityUid body)
    {
        _attribution.ClearAttribution(body);

        if (!TryComp<FSMedicalPatientComponent>(body, out var comp))
            return;

        comp.HealedSinceReset.Clear();
        comp.EnemyCaused.Clear();
        comp.PendingSaveCredit = null;
    }

    private bool TryGetPlayerMind(EntityUid? actor, out EntityUid mindId)
    {
        mindId = default;
        return actor is { } uid
               && _mind.TryGetMind(uid, out mindId, out var mind)
               && mind.UserId != null;
    }

    private void OnPatientDamageChanged(EntityUid uid, FSMedicalPatientComponent comp, DamageChangedEvent args)
    {
        if (args.DamageDelta is null || args.DamageDelta.Empty || _ailments.InInternalDamage)
            return;

        if (args.DamageIncreased)
        {
            if (!TryGetPlayerMind(args.Origin, out _))
                _attribution.ClearAttribution(uid);

            return;
        }

        var healed = -(float)args.DamageDelta.GetTotal();
        if (healed <= 0f)
            return;

        if (!TryGetPlayerMind(args.Origin, out var healerMind)
            && !_attribution.TryGetAttributedMedic(uid, out healerMind))
            return;

        if (!_mind.TryGetMind(uid, out var healedMind, out _) || healedMind != healerMind)
        {
            var allyHealed = new FSAllyHealedEvent(healerMind, uid, healed);
            RaiseLocalEvent(ref allyHealed);
        }

        if (healed < 1f)
            return;

        var preHeal = (float)_damageable.GetTotalDamage(uid) + healed;
        if (preHeal < FSMedicalPayoutRates.MinPreHealDamage)
            return;

        var prior = comp.HealedSinceReset.GetValueOrDefault(healerMind);
        var fullTier = Math.Clamp(FSMedicalPayoutRates.HealFullBudget - prior, 0f, healed);
        var halfTier = Math.Clamp(FSMedicalPayoutRates.HealHalfBudget - MathF.Max(prior, FSMedicalPayoutRates.HealFullBudget), 0f, healed - fullTier);
        var paid = fullTier + halfTier * FSMedicalPayoutRates.HealHalfRate;

        comp.HealedSinceReset[healerMind] = prior + healed;

        if (_mind.TryGetMind(uid, out var patientMind, out _) && patientMind == healerMind)
            return;

        var points = (int)MathF.Round(paid);
        if (points <= 0)
            return;

        var credits = Award(healerMind, "healing",
            new FSMedicalLine(points, points * FSMedicalPayoutRates.CreditsPerHealPoint, points * FSMedicalPayoutRates.FundPerHealPoint),
            hpHealed: healed);

        if (TryComp<MindComponent>(healerMind, out var healerMindComp)
            && healerMindComp.UserId is { } userId
            && _players.TryGetSessionById(userId, out var session))
        {
            RaiseNetworkEvent(new FSHealPayoutEvent(credits, prior >= FSMedicalPayoutRates.HealFullBudget), session);
        }

        AwardSupplier(uid, healerMind, paid);

        if (_damageable.GetTotalDamage(uid) <= 0)
            _attribution.ClearAttribution(uid);
    }

    private void AwardSupplier(EntityUid patient, EntityUid healerMind, float paid)
    {
        if (!_attribution.TryGetAttributedSupplier(patient, out var supplierMind)
            || supplierMind == healerMind
            || !supplierMind.IsValid())
            return;

        var points = (int)MathF.Round(paid * FSMedicalPayoutRates.SupplierRate);
        if (points <= 0)
            return;

        Award(supplierMind, "chem-supply", new FSMedicalLine(
            points,
            points * FSMedicalPayoutRates.SupplierCreditsPerPoint,
            points * FSMedicalPayoutRates.SupplierFundPerPoint));
    }

    private void OnTopicalApplied(EntityUid uid, FSMedicalPatientComponent comp, ref FSTopicalAppliedEvent args)
    {
        _combatMedic.OnHealedAlly(args.User, args.Target);

        if (args.User == args.Target
            || !_roster.IsMedical(args.User)
            || !TryGetPlayerMind(args.User, out var medicMind)
            || !UnderCap(_topicalsPaid, medicMind, args.Target, FSMedicalPayoutRates.TopicalsPaidPerPatientPerWave))
            return;

        if (PayLine(medicMind, args.Target, FSMedicalPayoutRates.Topical, "topical"))
            CountCap(_topicalsPaid, medicMind, args.Target);
    }

    private void OnAilmentInflicted(ref FSAilmentInflictedEvent args)
    {
        if (!TryComp<FSMedicalPatientComponent>(args.Patient, out var patient))
            return;

        if (args.Source is { } source && HasComp<WaveSpawnedTagComponent>(source))
            patient.EnemyCaused.Add(args.Slot);
        else
            patient.EnemyCaused.Remove(args.Slot);
    }

    private void OnAilmentCured(ref FSAilmentCuredEvent args)
    {
        if (args.Kind is FSCureKind.Rider or FSCureKind.Wash)
            return;

        if (args.Curer is { } curer)
            _combatMedic.OnHealedAlly(curer, args.Patient);

        if (args.Tier == FSAilmentTier.None
            || !TryComp<FSMedicalPatientComponent>(args.Patient, out var patient)
            || !patient.EnemyCaused.Remove(args.Slot)
            || !TryGetCureMedic(args, out var medicMind)
            || (_mind.TryGetMind(args.Patient, out var patientMind, out _) && patientMind == medicMind))
            return;

        var line = CureLine(args);
        if (line.IsEmpty
            || !UnderCap(_curesPaid, medicMind, args.Patient, FSMedicalPayoutRates.CuresPaidPerPatientPerWave)
            || (args.Kind == FSCureKind.Mass && !TakeMassSlot(medicMind)))
            return;

        var source = args.Kind is FSCureKind.Automatic or FSCureKind.Mass ? "cure-automatic" : "cure-hands-on";
        if (!PayLine(medicMind, args.Patient, line, source, treated: 1))
            return;

        CountCap(_curesPaid, medicMind, args.Patient);
        _adminLogger.Add(LogType.Healed, LogImpact.Low,
            $"{ToPrettyString(medicMind):medic} cured {args.Slot} ({args.Tier}, {args.Kind}) on {ToPrettyString(args.Patient):patient} for {line.Credits} credits");
    }

    private bool TryGetCureMedic(FSAilmentCuredEvent args, out EntityUid medicMind)
    {
        if (args.Curer is { } curer)
            return TryGetPlayerMind(curer, out medicMind) && _roster.IsMedical(curer);

        return _attribution.TryGetAttributedMedic(args.Patient, out medicMind)
               && CompOrNull<MindComponent>(medicMind)?.OwnedEntity is { } body
               && _roster.IsMedical(body);
    }

    private FSMedicalLine CureLine(FSAilmentCuredEvent args)
    {
        var automatic = args.Kind is FSCureKind.Automatic or FSCureKind.Mass;
        if (automatic && args.Slot == FSAilmentSlot.Bleeding && args.Tier == FSAilmentTier.Minor)
            return default;

        var line = args.Tier == FSAilmentTier.Major ? FSMedicalPayoutRates.MajorCure : FSMedicalPayoutRates.MinorCure;
        var scale = automatic ? FSMedicalPayoutRates.AutomaticMultiplier : 1f;
        var kill = WaveGameRuleSystem.GetWaveKillMultiplier(_waveRule.GetWaveNumber());
        return line.Scale(kill * scale, scale);
    }

    private bool TakeMassSlot(EntityUid medicMind)
    {
        var now = _timing.CurTime;
        var slot = _massPaid.TryGetValue(medicMind, out var open) && open.Until > now
            ? open
            : (Count: 0, Until: now + FSMedicalPayoutRates.MassClearWindow);

        if (slot.Count >= FSMedicalPayoutRates.MassClearPatientsPaid)
            return false;

        _massPaid[medicMind] = (slot.Count + 1, slot.Until);
        return true;
    }

    private bool UnderCap(Dictionary<(EntityUid, EntityUid), (int Wave, int Count)> paid, EntityUid medicMind, EntityUid patient, int cap)
    {
        return !paid.TryGetValue((medicMind, patient), out var record)
               || record.Wave != _waveRule.GetWaveNumber()
               || record.Count < cap;
    }

    private void CountCap(Dictionary<(EntityUid, EntityUid), (int Wave, int Count)> paid, EntityUid medicMind, EntityUid patient)
    {
        var wave = _waveRule.GetWaveNumber();
        var key = (medicMind, patient);
        paid[key] = paid.TryGetValue(key, out var record) && record.Wave == wave
            ? (wave, record.Count + 1)
            : (wave, 1);
    }

    private bool PayLine(EntityUid medicMind, EntityUid patient, FSMedicalLine line, string source,
        int stabilises = 0, int revives = 0, int treated = 0)
    {
        RollPayTick();
        var key = (medicMind, patient);
        var already = _linesThisTick.GetValueOrDefault(key);
        _linesThisTick[key] = already.Max(line);

        var extra = line.Above(already);
        if (extra.IsEmpty && stabilises == 0 && revives == 0 && treated == 0)
            return false;

        Award(medicMind, source, extra, stabilises: stabilises, revives: revives, treated: treated);
        return !extra.IsEmpty;
    }

    private void RollPayTick()
    {
        if (_payTick == _timing.CurTick)
            return;

        _payTick = _timing.CurTick;
        _linesThisTick.Clear();
    }

    private static bool IsDownward(MobState oldState, MobState newState)
        => (newState == MobState.Critical && oldState == MobState.Alive)
           || (newState == MobState.Dead && oldState is MobState.Alive or MobState.Critical);

    private void OnPatientMobStateChanged(EntityUid uid, FSMedicalPatientComponent comp, ref MobStateChangedEvent args)
    {
        if (IsDownward(args.OldMobState, args.NewMobState))
            comp.PendingSaveCredit = null;

        if (args.Origin == uid || !TryGetPlayerMind(args.Origin, out var healerMind))
            return;

        if (args.OldMobState == MobState.Dead && args.NewMobState is MobState.Critical or MobState.Alive)
        {
            PayLine(healerMind, uid, Prioritised(uid, FSMedicalPayoutRates.Revive), "revive", revives: 1);

            var revived = new FSAllyRevivedEvent(healerMind, uid);
            RaiseLocalEvent(ref revived);

            comp.PendingSaveCredit = healerMind;
            comp.PendingSaveWave = _waveRule.GetWaveNumber();
            comp.PendingSaveAt = _timing.CurTime;

            _adminLogger.Add(LogType.Healed, LogImpact.Medium,
                $"{ToPrettyString(healerMind):healer} revived {ToPrettyString(uid):patient} for {FSMedicalPayoutRates.Revive.Credits} credits");
            return;
        }

        if (args.OldMobState == MobState.Critical && args.NewMobState == MobState.Alive)
        {
            PayLine(healerMind, uid, Prioritised(uid, FSMedicalPayoutRates.Stabilise), "stabilise", stabilises: 1);

            _adminLogger.Add(LogType.Healed, LogImpact.Low,
                $"{ToPrettyString(healerMind):healer} stabilised {ToPrettyString(uid):patient} for {FSMedicalPayoutRates.Stabilise.Credits} credits");
        }
    }

    private FSMedicalLine Prioritised(EntityUid patient, FSMedicalLine line)
    {
        return _casualty.IsPriority(patient)
            ? line.Scale(FSMedicalPayoutRates.PriorityMultiplier, FSMedicalPayoutRates.PriorityMultiplier)
            : line;
    }

    private void OnWaveEnded(ref WaveEndedEvent args)
    {
        var query = EntityQueryEnumerator<FSMedicalPatientComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            if (comp.PendingSaveCredit is not { } reviver)
                continue;

            comp.PendingSaveCredit = null;

            if (!reviver.IsValid() || comp.PendingSaveWave != args.WaveNumber)
                continue;
            if (_mobState.IsIncapacitated(uid))
                continue;
            if (_timing.CurTime - comp.PendingSaveAt < MinSurvivalForSave)
                continue;

            Award(reviver, "patient-saved", FSMedicalPayoutRates.PatientSaved, patientsSaved: 1);

            _adminLogger.Add(LogType.Healed, LogImpact.Low,
                $"{ToPrettyString(uid):patient} survived wave {args.WaveNumber} — patient saved credited to {ToPrettyString(reviver):reviver}");
        }

        LogIncome(args.WaveNumber);
    }

    private int Award(EntityUid mindId, string source, FSMedicalLine line, float hpHealed = 0f,
        int stabilises = 0, int revives = 0, int patientsSaved = 0, int treated = 0)
    {
        var cur = _roundStats.GetValueOrDefault(mindId);
        _roundStats[mindId] = new FSMedicalRoundStats(
            cur.HealingPoints + line.Points,
            cur.HpHealed + hpHealed,
            cur.Stabilises + stabilises,
            cur.Revives + revives,
            cur.PatientsSaved + patientsSaved,
            cur.AilmentsTreated + treated);

        var job = JobOf(mindId);
        var multiplier = job != null && FSMedicalPayoutRates.JobMultipliers.TryGetValue(job, out var jobScale) ? jobScale : 1f;
        var credits = (int) MathF.Round(line.Credits * multiplier);
        if (credits > 0)
            _wallet.GiveCredits(mindId, credits);

        if (line.Fund > 0)
            _medFund.GrantMedicalFunds(line.Fund, source, mindId);

        var key = (job ?? "None", source);
        var income = _waveIncome.GetValueOrDefault(key);
        _waveIncome[key] = (income.Credits + credits, income.Fund + Math.Max(0, line.Fund));

        return credits;
    }

    private string? JobOf(EntityUid mindId)
    {
        return CompOrNull<MindComponent>(mindId)?.OwnedEntity is { } body ? _roster.JobOf(body) : null;
    }

    private void LogIncome(int wave)
    {
        if (_waveIncome.Count == 0)
            return;

        var lines = _waveIncome
            .OrderBy(kv => kv.Key.Job)
            .ThenBy(kv => kv.Key.Source)
            .Select(kv => $"{kv.Key.Job}/{kv.Key.Source} {kv.Value.Credits}c {kv.Value.Fund}f");

        Log.Info($"[FSMedParity] wave {wave}: {string.Join(", ", lines)}");
        _waveIncome.Clear();
    }

    private void OnRoundEnd(RoundEndTextAppendEvent args)
    {
        var sorted = _roundStats
            .Where(kv => kv.Key.IsValid())
            .OrderByDescending(kv => kv.Value.HealingPoints)
            .ToList();

        if (sorted.Count == 0)
            return;

        args.AddLine("══ MEDICAL ═══════════════════════════════════");
        foreach (var (mindId, stats) in sorted)
        {
            if (!TryComp<MindComponent>(mindId, out var mind)) continue;
            var name = mind.CharacterName ?? "Unknown";
            args.AddLine($"  {name,-16} {stats.HealingPoints,7:N0} HP   {stats.Revives} revives  {stats.Stabilises} stabilised  {stats.PatientsSaved} saved  {stats.AilmentsTreated} treated");
        }
        args.AddLine($"  Department funds earned: ${_medFund.GetLifetimeEarned():N0}");
        args.AddLine("══════════════════════════════════════════════");
    }
}
