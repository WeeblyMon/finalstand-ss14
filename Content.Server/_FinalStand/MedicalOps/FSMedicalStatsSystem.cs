using System.Linq;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server._Shitmed.Body.Systems;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.GameTicking;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.MedicalOps;

// Medical's answer to kills and assists: credit for the medical work you actually performed.
public sealed partial class FSMedicalStatsSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSTreatmentAttributionSystem _attribution = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;

    private const float DrFullBudget = 60f;
    private const float DrHalfBudget = 150f;
    private const float DrHalfRate = 0.35f;
    private const float DrResetDamage = 25f;
    private const float MinPreHealDamage = 10f;

    private const float SelfHealRate = 0.5f;

    private const int CreditsPerHealPoint = 10;
    private const int SelfHealCreditsPerPoint = 2;

    private const int StabilisePoints = 25;
    private const int StabiliseCredits = 250;
    private const int RevivePoints = 60;
    private const int ReviveCredits = 500;
    private const int PatientSavedCredits = 1000;

    public readonly record struct FSMedicalRoundStats(
        int HealingPoints, float HpHealed, int Stabilises, int Revives, int PatientsSaved);

    private readonly Dictionary<EntityUid, FSMedicalRoundStats> _roundStats = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSMedicalPatientComponent, DamageChangedEvent>(
            OnPatientDamageChanged, after: [typeof(BodyDamageRouterSystem)]);
        SubscribeLocalEvent<FSMedicalPatientComponent, MobStateChangedEvent>(OnPatientMobStateChanged);

        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd, after: [typeof(FSLevelingSystem)]);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev) => EnsureComp<FSMedicalPatientComponent>(ev.Mob);

    private void OnPlayerAttached(PlayerAttachedEvent ev) => EnsureComp<FSMedicalPatientComponent>(ev.Entity);

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev) => _roundStats.Clear();

    private void OnPrepStarted(WavePrepStartedEvent ev)
    {
        var query = EntityQueryEnumerator<FSMedicalPatientComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            comp.HealedSinceReset.Clear();
            comp.DamageSinceReset = 0f;
        }
    }

    public void ResetPatient(EntityUid body)
    {
        if (!TryComp<FSMedicalPatientComponent>(body, out var comp))
            return;

        comp.HealedSinceReset.Clear();
        comp.DamageSinceReset = 0f;
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
        if (args.DamageDelta is null || args.DamageDelta.Empty)
            return;

        if (args.DamageIncreased)
        {
            // Only hostile damage refills the budget; shoot-then-heal is the farming vector.
            if (TryGetPlayerMind(args.Origin, out _))
                return;

            comp.DamageSinceReset += (float)args.DamageDelta.GetTotal();
            if (comp.DamageSinceReset >= DrResetDamage)
            {
                comp.HealedSinceReset.Clear();
                comp.DamageSinceReset = 0f;
            }
            return;
        }

        var healed = -(float)args.DamageDelta.GetTotal();
        if (healed < 1f)
            return;

        // Reagents metabolise long after the syringe left the medic's hand, so they carry no origin.
        if (!TryGetPlayerMind(args.Origin, out var healerMind)
            && !_attribution.TryGetAttributedMedic(uid, out healerMind))
            return;

        var preHeal = (float)_damageable.GetTotalDamage(uid) + healed;
        if (preHeal < MinPreHealDamage)
            return;

        var prior = comp.HealedSinceReset.GetValueOrDefault(healerMind);
        var fullTier = Math.Clamp(DrFullBudget - prior, 0f, healed);
        var halfTier = Math.Clamp(DrHalfBudget - MathF.Max(prior, DrFullBudget), 0f, healed - fullTier);
        var paid = fullTier + halfTier * DrHalfRate;

        comp.HealedSinceReset[healerMind] = prior + healed;

        var isSelf = _mind.TryGetMind(uid, out var patientMind, out _) && patientMind == healerMind;
        if (isSelf)
            paid *= SelfHealRate;

        var points = (int)MathF.Round(paid);
        if (points <= 0)
            return;

        AddStats(healerMind, points: points, hpHealed: healed);
        _wallet.GiveCredits(healerMind, points * (isSelf ? SelfHealCreditsPerPoint : CreditsPerHealPoint));
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

        // A defibrillator lands the patient in Critical, never straight to Alive.
        if (args.OldMobState == MobState.Dead && args.NewMobState is MobState.Critical or MobState.Alive)
        {
            AddStats(healerMind, points: RevivePoints, revives: 1);
            _wallet.GiveCredits(healerMind, ReviveCredits);

            comp.PendingSaveCredit = healerMind;
            comp.PendingSaveWave = _waveRule.TryGetActiveState(out var wave) ? wave.WaveNumber : 0;
            return;
        }

        if (args.OldMobState == MobState.Critical && args.NewMobState == MobState.Alive)
        {
            AddStats(healerMind, points: StabilisePoints, stabilises: 1);
            _wallet.GiveCredits(healerMind, StabiliseCredits);
        }
    }

    private void OnWaveEnded(ref WaveEndedEvent args)
    {
        // Keyed on the patient, so die-revive-die cannot mint several saves off one body.
        var savedPatients = new HashSet<EntityUid>();

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
            if (!savedPatients.Add(uid))
                continue;

            AddStats(reviver, patientsSaved: 1);
            _wallet.GiveCredits(reviver, PatientSavedCredits);
        }
    }

    private void AddStats(EntityUid mindId, int points = 0, float hpHealed = 0f,
        int stabilises = 0, int revives = 0, int patientsSaved = 0)
    {
        var cur = _roundStats.GetValueOrDefault(mindId);
        _roundStats[mindId] = new FSMedicalRoundStats(
            cur.HealingPoints + points,
            cur.HpHealed + hpHealed,
            cur.Stabilises + stabilises,
            cur.Revives + revives,
            cur.PatientsSaved + patientsSaved);
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
            args.AddLine($"  {name,-16} {stats.HealingPoints,7:N0} HP   {stats.Revives} revives  {stats.Stabilises} stabilised  {stats.PatientsSaved} saved");
        }
        args.AddLine("══════════════════════════════════════════════");
    }
}
