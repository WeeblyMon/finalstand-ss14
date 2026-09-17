using Content.Server.Administration.Logs;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.GameTicking;
using Content.Shared.Database;
using Content.Shared.Mind;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

public sealed class FSChemCreditSystem : EntitySystem
{
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;

    private static readonly TimeSpan ClaimLifetime = TimeSpan.FromSeconds(120);


    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(5);

    private readonly Dictionary<EntityUid, Claim> _claims = new();
    private readonly List<EntityUid> _expired = new();
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

    public void RegisterDelivery(EntityUid target, EntityUid deployer, int? budget = null)
    {
        if (target == deployer
            || !HasComp<FSFriendlyFireComponent>(target)
            || !_mind.TryGetMind(deployer, out var mindId, out _))
        {
            return;
        }

        _claims[target] = new Claim
        {
            SupplierMind = mindId,
            Expires = _timing.CurTime + ClaimLifetime,
            Remaining = budget ?? FSMedicalPayoutRates.ChemClaimBudget,
        };
    }

    private void OnWaveEnemyDied(ref FSWaveEnemyDiedEvent args)
    {
        if (args.Killer is not { } killer || !_claims.TryGetValue(killer, out var claim))
            return;

        if (claim.Expires <= _timing.CurTime || claim.Remaining <= 0)
        {
            _claims.Remove(killer);
            return;
        }

        var baseCredits = TryComp<FSEnemyValueComponent>(args.Enemy, out var value)
            ? value.KillCredits
            : 100;

        var cut = Math.Min((int) MathF.Round(baseCredits * FSMedicalPayoutRates.SupplierRate), claim.Remaining);
        if (cut <= 0)
            return;

        _wallet.GiveCredits(claim.SupplierMind, cut);

        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(claim.SupplierMind):chemist} earned {cut} supply credit from a buffed kill");

        claim.Remaining -= cut;
        if (claim.Remaining <= 0)
            _claims.Remove(killer);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextSweep)
            return;

        _nextSweep = now + SweepInterval;

        _expired.Clear();
        foreach (var (target, claim) in _claims)
        {
            if (claim.Expires <= now)
                _expired.Add(target);
        }

        foreach (var target in _expired)
            _claims.Remove(target);
    }

    private sealed class Claim
    {
        public EntityUid SupplierMind;
        public TimeSpan Expires;
        public int Remaining;
    }
}
