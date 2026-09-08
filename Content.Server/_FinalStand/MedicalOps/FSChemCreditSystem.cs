using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

public sealed class FSChemCreditSystem : EntitySystem
{
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan ClaimLifetime = TimeSpan.FromSeconds(120);

    private const float SupplierRate = 0.3f;
    private const int ClaimBudget = 400;

    private readonly Dictionary<EntityUid, Claim> _claims = new();
    private readonly List<EntityUid> _expired = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnEnemyStateChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _claims.Clear();
    }

    public void RegisterDelivery(EntityUid target, EntityUid deployer)
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
            Remaining = ClaimBudget,
        };
    }

    private void OnEnemyStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        if (!HasComp<WaveSpawnedTagComponent>(args.Target))
            return;

        if (args.Origin is not { } killer || !_claims.TryGetValue(killer, out var claim))
            return;

        if (claim.Expires <= _timing.CurTime || claim.Remaining <= 0)
        {
            _claims.Remove(killer);
            return;
        }

        var baseCredits = TryComp<FSEnemyValueComponent>(args.Target, out var value)
            ? value.KillCredits
            : 100;

        var cut = Math.Min((int) MathF.Round(baseCredits * SupplierRate), claim.Remaining);
        if (cut <= 0)
            return;

        _wallet.GiveCredits(claim.SupplierMind, cut);

        claim.Remaining -= cut;
        if (claim.Remaining <= 0)
            _claims.Remove(killer);
        else
            _claims[killer] = claim;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        _expired.Clear();
        foreach (var (target, claim) in _claims)
        {
            if (claim.Expires <= now)
                _expired.Add(target);
        }

        foreach (var target in _expired)
            _claims.Remove(target);
    }

    private struct Claim
    {
        public EntityUid SupplierMind;
        public TimeSpan Expires;
        public int Remaining;
    }
}
