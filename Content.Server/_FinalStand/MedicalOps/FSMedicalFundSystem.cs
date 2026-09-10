using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSMedicalFundSystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerManager = default!;


    private const float NotifyInterval = 0.25f;

    private EntityUid? _fund;
    private bool _dirty;
    private float _notifyAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMedicalFundComponent, EntityTerminatingEvent>(OnFundTerminating);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeNetworkEvent<FSMedicalFundRequestEvent>(OnFundRequested);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_dirty)
            return;

        _notifyAccumulator += frameTime;
        if (_notifyAccumulator < NotifyInterval)
            return;

        _notifyAccumulator = 0f;
        _dirty = false;
        var balance = GetBalance();
        RaiseNetworkEvent(new FSMedicalFundUpdatedEvent(balance), Filter.Broadcast());

        var changed = new FSMedicalFundBalanceChangedEvent(balance);
        RaiseLocalEvent(ref changed);
    }

    private Entity<FSMedicalFundComponent> GetOrCreateFund()
    {
        if (_fund is { } cached && Exists(cached) && TryComp<FSMedicalFundComponent>(cached, out var cachedComp))
            return (cached, cachedComp);

        var query = EntityQueryEnumerator<FSMedicalFundComponent>();
        if (query.MoveNext(out var existing, out var existingComp))
        {
            _fund = existing;
            return (existing, existingComp);
        }

        var spawned = Spawn(null, MapCoordinates.Nullspace);
        var comp = AddComp<FSMedicalFundComponent>(spawned);
        _fund = spawned;
        return (spawned, comp);
    }

    private void OnFundTerminating(EntityUid uid, FSMedicalFundComponent comp, ref EntityTerminatingEvent args)
    {
        if (_fund == uid)
            _fund = null;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _fund = null;

        var query = EntityQueryEnumerator<FSMedicalFundComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            comp.Balance = 0;
            comp.LifetimeEarned = 0;
            comp.ContributionByMind.Clear();
        }

        _dirty = true;
    }

    private void OnFundRequested(FSMedicalFundRequestEvent ev, EntitySessionEventArgs args)
    {
        RaiseNetworkEvent(new FSMedicalFundUpdatedEvent(GetBalance()), args.SenderSession);
    }

    public void GrantMedicalFunds(int amount, string source, EntityUid? contributorMindId = null)
    {
        if (amount <= 0)
            return;

        var fund = GetOrCreateFund();
        fund.Comp.Balance += amount;
        fund.Comp.LifetimeEarned += amount;

        if (contributorMindId is { } mindId)
        {
            fund.Comp.ContributionByMind.TryGetValue(mindId, out var prior);
            fund.Comp.ContributionByMind[mindId] = prior + amount;
        }

        _dirty = true;
        Log.Debug($"[FSMedFund] +{amount} ({source}) — balance {fund.Comp.Balance}");
    }

    public bool TryDeductMedicalFunds(int amount)
    {
        if (amount <= 0)
            return false;

        var fund = GetOrCreateFund();
        if (fund.Comp.Balance < amount)
            return false;

        fund.Comp.Balance -= amount;
        _dirty = true;
        return true;
    }

    public int GetBalance() => GetOrCreateFund().Comp.Balance;

    public int GetLifetimeEarned() => GetOrCreateFund().Comp.LifetimeEarned;

    public IReadOnlyDictionary<EntityUid, int> GetContributions() => GetOrCreateFund().Comp.ContributionByMind;

    public void DumpFund(IConsoleShell shell)
    {
        var fund = GetOrCreateFund();
        shell.WriteLine($"  balance={fund.Comp.Balance}  lifetimeEarned={fund.Comp.LifetimeEarned}");

        if (fund.Comp.ContributionByMind.Count == 0)
        {
            shell.WriteLine("  (no contributors yet)");
            return;
        }

        foreach (var (mindId, amount) in fund.Comp.ContributionByMind)
        {
            var name = TryComp<MindComponent>(mindId, out var mind) ? mind.CharacterName ?? "Unknown" : "Unknown";
            shell.WriteLine($"    {name,-16} {amount,7:N0}");
        }
    }
}
