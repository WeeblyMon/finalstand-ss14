using Content.Server.Administration.Logs;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.GameTicking;
using Content.Shared.Database;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

// Pays chemists for kills they enabled: a cut when a crewmate they buffed scores, and an assist when a zombie they softened dies to someone else.
public sealed partial class FSChemCreditSystem : EntitySystem
{
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private FSMedicalFundSystem _medFund = default!;
    [Dependency] private WaveGameRuleSystem _wave = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private FSChemLauncherSystem _launcher = default!;
    [Dependency] private FSMedicalStatsSystem _stats = default!;

    private static readonly TimeSpan ClaimLifetime = TimeSpan.FromSeconds(120);

    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(5);

    private const float AssistRate = 0.25f;
    private const float AssistCatalyst = 3f;
    private const float AssistDamageShare = 0.15f;

    private readonly Dictionary<(EntityUid Target, EntityUid Supplier), Claim> _claims = new();
    private readonly List<(EntityUid, EntityUid)> _expired = new();
    private TimeSpan _nextSweep;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSWaveEnemyDiedEvent>(OnWaveEnemyDied);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _claims.Clear();
    }

    // Re-dosing the same crewmate keeps the claim alive but never refills what this wave has already paid out.
    public void RegisterDelivery(EntityUid target, EntityUid deployer, int? budget = null)
    {
        if (target == deployer
            || !HasComp<FSFriendlyFireComponent>(target)
            || !_mind.TryGetMind(deployer, out var mindId, out _))
        {
            return;
        }

        var key = (target, mindId);
        var wave = _wave.GetWaveNumber();
        var allowance = budget ?? FSMedicalPayoutRates.ChemClaimBudget;

        if (!_claims.TryGetValue(key, out var claim) || claim.Wave != wave)
        {
            claim = new Claim { Wave = wave };
            _claims[key] = claim;
        }

        claim.Budget = Math.Max(claim.Budget, allowance);
        claim.Expires = _timing.CurTime + ClaimLifetime;
    }

    private void OnWaveEnemyDied(ref FSWaveEnemyDiedEvent args)
    {
        var baseCredits = TryComp<FSEnemyValueComponent>(args.Enemy, out var value)
            ? value.KillCredits
            : 100;

        if (args.Killer is { } killer)
            PayBuffedKill(killer, baseCredits);

        PayAssist(args.Enemy, args.Killer, baseCredits);
    }

    private void PayBuffedKill(EntityUid killer, int baseCredits)
    {
        var now = _timing.CurTime;
        EntityUid? supplier = null;
        Claim? best = null;

        foreach (var ((target, mind), claim) in _claims)
        {
            if (target != killer || claim.Expires <= now || claim.Remaining <= 0)
                continue;

            if (best == null || claim.Expires > best.Expires)
            {
                best = claim;
                supplier = mind;
            }
        }

        if (best == null || supplier is not { } supplierMind)
            return;

        var cut = Math.Min((int) MathF.Round(baseCredits * FSMedicalPayoutRates.SupplierRate), best.Remaining);
        if (cut <= 0)
            return;

        best.Paid += cut;
        Pay(supplierMind, cut, "chem-buff");

        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(supplierMind):chemist} earned {cut} supply credit from a buffed kill");
    }

    private void PayAssist(EntityUid enemy, EntityUid? killer, int baseCredits)
    {
        if (!TryComp<FSChemTaggedComponent>(enemy, out var tag) || tag.Until <= _timing.CurTime)
            return;

        _launcher.AddCatalyst(tag.Launcher, AssistCatalyst);

        if (killer == tag.Chemist || !_mind.TryGetMind(tag.Chemist, out var mindId, out _))
            return;

        if (!tag.Debuffed
            && (!_thresholds.TryGetThresholdForState(enemy, MobState.Dead, out var dead)
                || tag.Damage < dead.Value.Float() * AssistDamageShare))
            return;

        var cut = (int) MathF.Round(baseCredits * AssistRate);
        if (cut <= 0)
            return;

        Pay(mindId, cut, "chem-assist");
        _stats.RecordChem(mindId, assisted: 1);

        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(mindId):chemist} earned {cut} assist credit on {ToPrettyString(enemy):enemy}");
    }

    private void Pay(EntityUid mind, int credits, string source)
    {
        _wallet.GiveCredits(mind, credits);
        _medFund.GrantMedicalFunds(
            credits * FSMedicalPayoutRates.SupplierFundPerPoint / FSMedicalPayoutRates.SupplierCreditsPerPoint,
            source, mind);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextSweep)
            return;

        _nextSweep = now + SweepInterval;

        var wave = _wave.GetWaveNumber();
        _expired.Clear();
        foreach (var (key, claim) in _claims)
        {
            if (claim.Wave != wave)
                _expired.Add(key);
        }

        foreach (var key in _expired)
            _claims.Remove(key);
    }

    private sealed class Claim
    {
        public int Wave;
        public TimeSpan Expires;
        public int Budget;
        public int Paid;

        public int Remaining => Budget - Paid;
    }
}
