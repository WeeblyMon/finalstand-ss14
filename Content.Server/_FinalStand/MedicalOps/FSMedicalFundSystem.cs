using System.Collections.Frozen;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSMedicalFundSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;

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

    /// <summary>Resolves the fund without creating one. Reads must use this.</summary>
    private bool TryGetFund(out Entity<FSMedicalFundComponent> fund)
    {
        if (_fund is { } cached && Exists(cached) && TryComp<FSMedicalFundComponent>(cached, out var cachedComp))
        {
            fund = (cached, cachedComp);
            return true;
        }

        var query = EntityQueryEnumerator<FSMedicalFundComponent>();
        if (query.MoveNext(out var existing, out var existingComp))
        {
            _fund = existing;
            fund = (existing, existingComp);
            return true;
        }

        fund = default;
        return false;
    }

    private Entity<FSMedicalFundComponent> EnsureFund()
    {
        if (TryGetFund(out var fund))
            return fund;

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

        var fund = EnsureFund();
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

        if (!TryGetFund(out var fund) || fund.Comp.Balance < amount)
            return false;

        fund.Comp.Balance -= amount;
        _dirty = true;

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"Medical fund spent {amount} - balance {fund.Comp.Balance}");
        return true;
    }

    public int GetBalance() => TryGetFund(out var fund) ? fund.Comp.Balance : 0;

    public int GetLifetimeEarned() => TryGetFund(out var fund) ? fund.Comp.LifetimeEarned : 0;

    public IReadOnlyDictionary<EntityUid, int> GetContributions() =>
        TryGetFund(out var fund) ? fund.Comp.ContributionByMind : FrozenDictionary<EntityUid, int>.Empty;

    public void DumpFund(IConsoleShell shell)
    {
        if (!TryGetFund(out var fund))
        {
            shell.WriteLine("  (no fund yet)");
            return;
        }

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
