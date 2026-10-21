using System.Text.Json;
using Content.Server._FinalStand.Perks;
using Content.Server._FinalStand.Leveling;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Leveling;
using Content.Shared.GameTicking;
using Content.Server.GameTicking;
using Content.Shared.Mind;
using Robust.Shared.Console;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Economy;

public sealed partial class FSPlayerWalletSystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private FSPlayerDataStore _store = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan LobbyRequestInterval = TimeSpan.FromSeconds(1);

    private readonly Dictionary<NetUserId, string> _cachedUsernames = new();
    private readonly Dictionary<NetUserId, TimeSpan> _lastLobbyRequest = new();

    private readonly HashSet<EntityUid> _dirtyWallets = new();

    // Players who already got their starting credits this round. Per player, because a cryo return makes a new mind.
    private readonly HashSet<NetUserId> _startingCreditsGiven = new();
    private float _notifyAccumulator;
    private const float NotifyInterval = 0.25f;

    private const int StartingCredits = 500;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeNetworkEvent<WalletRequestEvent>(OnWalletRequested);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_dirtyWallets.Count == 0)
            return;

        _notifyAccumulator += frameTime;
        if (_notifyAccumulator < NotifyInterval)
            return;
        _notifyAccumulator = 0f;

        foreach (var mindId in _dirtyWallets)
        {
            if (TryComp<FSPlayerWalletComponent>(mindId, out var wallet))
                PushWalletState(mindId, wallet);
        }
        _dirtyWallets.Clear();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _lastLobbyRequest.Clear();
        _startingCreditsGiven.Clear();
        SaveAll();
        _dirtyWallets.Clear();

        var query = EntityQueryEnumerator<FSPlayerWalletComponent>();
        while (query.MoveNext(out _, out var wallet))
        {
            wallet.Credits = 0;
            wallet.Loaded = false;
        }
        Log.Debug("Round restart — saved all players, cleared credits.");
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!_mind.TryGetMind(ev.Player, out var mindId, out var mind) || mind.UserId == null)
            return;

        _cachedUsernames[mind.UserId.Value] = ev.Player.Name;

        if (TryComp<FSPlayerWalletComponent>(mindId, out var wallet))
            MarkWalletDirty(mindId);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!_mind.TryGetMind(ev.Mob, out var mindId, out _))
            return;

        _cachedUsernames[ev.Player.UserId] = ev.Player.Name;

        var wallet = GetLoadedWallet(mindId, ev.Player.UserId);
        if (_startingCreditsGiven.Add(ev.Player.UserId))
            wallet.Credits += StartingCredits;

        MarkWalletDirty(mindId);
    }

    // Reads the saved row before anything touches the perk points. A wallet created by an early payout
    // (an observer at a boss kill, a pay-out in the same spawn event) would otherwise save that payout
    // over the player's real total. If the read fails the wallet stays unloaded and unsaved, keeping what it
    // earns meanwhile on top of the row once a later read works.
    private FSPlayerWalletComponent GetLoadedWallet(EntityUid mindId, NetUserId user)
    {
        var wallet = EnsureComp<FSPlayerWalletComponent>(mindId);
        if (wallet.Loaded || !_store.TryGetFullRecord(user.UserId, out var record))
            return wallet;

        wallet.PerkPoints += record.PerkPoints;
        wallet.Loaded = true;
        return wallet;
    }

    private void SavePerkPoints(NetUserId user, FSPlayerWalletComponent wallet)
    {
        if (wallet.Loaded)
            _store.UpsertPerkPoints(user.UserId, ResolveUsername(user), wallet.PerkPoints);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (!_mind.TryGetMind(ev.Entity, out var mindId, out var mind) || mind.UserId == null)
            return;

        SaveMind(mindId, mind.UserId.Value);
    }

    // Every wallet a player can own is created loaded; this only skips ones reset at round restart.
    private FSPlayerWalletComponent GetWallet(EntityUid mindId)
    {
        return TryComp<MindComponent>(mindId, out var mind) && mind.UserId is { } user
            ? GetLoadedWallet(mindId, user)
            : EnsureComp<FSPlayerWalletComponent>(mindId);
    }

    private void SaveMind(EntityUid mindId, NetUserId userId)
    {
        // An unloaded wallet's perk points aren't the player's; saving them would overwrite the real total.
        if (!TryComp<FSPlayerWalletComponent>(mindId, out var wallet) || !wallet.Loaded)
            return;

        _store.UpsertPerkPoints(userId.UserId, ResolveUsername(userId), wallet.PerkPoints);
    }

    private string ResolveUsername(NetUserId userId)
    {
        if (_playerManager.TryGetSessionById(userId, out var session))
            return session.Name;
        return _cachedUsernames.TryGetValue(userId, out var cached) ? cached : "unknown";
    }

    public void DistributeCredits(int amount)
    {
        var count = 0;
        foreach (var session in _playerManager.Sessions)
        {
            if (!_mind.TryGetMind(session, out var mindId, out _))
                continue;

            var wallet = GetLoadedWallet(mindId, session.UserId);
            wallet.Credits += amount;
            MarkWalletDirty(mindId);
            count++;
        }
        Log.Debug($"DistributeCredits +{amount} → {count} player(s)");
    }

    public void DistributePerkPoints(int amount)
    {
        var count = 0;
        foreach (var session in _playerManager.Sessions)
        {
            if (!_mind.TryGetMind(session, out var mindId, out _))
                continue;

            var wallet = GetLoadedWallet(mindId, session.UserId);
            wallet.PerkPoints += amount;
            MarkWalletDirty(mindId);
            SavePerkPoints(session.UserId, wallet);
            count++;
        }
        Log.Debug($"DistributePerkPoints +{amount} → {count} player(s)");
    }

    public void AddPerkPoints(EntityUid mindId, int amount)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId is not { } user)
            return;

        var wallet = GetLoadedWallet(mindId, user);
        wallet.PerkPoints += amount;
        MarkWalletDirty(mindId);
        SavePerkPoints(user, wallet);
    }

    public bool TryDeductPerkPoints(EntityUid mindId, int cost)
    {
        if (!TryComp<FSPlayerWalletComponent>(mindId, out var wallet) || wallet.PerkPoints < cost)
            return false;
        wallet.PerkPoints -= cost;
        MarkWalletDirty(mindId);
        if (TryComp<MindComponent>(mindId, out var mind) && mind.UserId is { } user)
            SavePerkPoints(user, wallet);
        return true;
    }

    public void GiveCredits(EntityUid mindId, int amount)
    {
        var wallet = GetWallet(mindId);
        wallet.Credits += amount;
        MarkWalletDirty(mindId);
        if (amount <= 0)
            return;

        var ev = new FSCreditsGivenEvent(amount);
        RaiseLocalEvent(mindId, ref ev);
    }

    public bool TryDeductCredits(EntityUid mindId, int amount)
    {
        // A negative charge would quietly pay out; callers refund through GiveCredits.
        if (amount < 0 || !TryComp<FSPlayerWalletComponent>(mindId, out var wallet) || wallet.Credits < amount)
            return false;
        wallet.Credits -= amount;
        MarkWalletDirty(mindId);
        return true;
    }

    public int DeductUpTo(EntityUid mindId, int amount)
    {
        if (amount <= 0)
            return 0;

        var wallet = GetWallet(mindId);
        var taken = Math.Min(wallet.Credits, amount);
        if (taken <= 0)
            return 0;

        wallet.Credits -= taken;
        MarkWalletDirty(mindId);
        return taken;
    }

    public int GetCredits(EntityUid mindId)
    {
        return TryComp<FSPlayerWalletComponent>(mindId, out var wallet) ? wallet.Credits : 0;
    }

    public void SaveAll()
    {
        var count = 0;
        _store.RunBatch("SaveAll", () =>
        {
            foreach (var session in _playerManager.Sessions)
            {
                if (!_mind.TryGetMind(session, out var mindId, out _))
                    continue;

                SaveMind(mindId, session.UserId);
                count++;
            }
        });

        Log.Debug($"SaveAll flushed {count} player(s)");
    }

    public int GetStoredPerkPoints(Guid userId)
    {
        if (_playerManager.TryGetSessionById(new NetUserId(userId), out var session)
            && _mind.TryGetMind(session, out var mindId, out _))
        {
            return GetPerkPoints(mindId, session.UserId);
        }

        return _store.GetPerkPoints(userId);
    }

    // Live wallet in round, the saved row otherwise (or for a wallet that hasn't been loaded).
    public int GetPerkPoints(EntityUid mindId, NetUserId user)
    {
        return TryComp<FSPlayerWalletComponent>(mindId, out var wallet) && wallet.Loaded
            ? wallet.PerkPoints
            : _store.GetPerkPoints(user.UserId);
    }

    public void GivePerkPoints(ICommonSession session, int amount)
    {
        if (_mind.TryGetMind(session, out var mindId, out _)
            && HasComp<FSPlayerWalletComponent>(mindId))
        {
            var wallet = GetLoadedWallet(mindId, session.UserId);
            wallet.PerkPoints = Math.Max(0, wallet.PerkPoints + amount);
            MarkWalletDirty(mindId);
            SavePerkPoints(session.UserId, wallet);
            return;
        }

        if (!_store.TryGetPerkPoints(session.UserId.UserId, out var current))
            return;

        var updated = Math.Max(0, current + amount);
        _store.UpsertPerkPoints(session.UserId.UserId, session.Name, updated);
        RaiseNetworkEvent(new WalletUpdatedEvent(0, updated), Filter.SinglePlayer(session));
    }

    public int WipeAllPrestige()
    {
        var deleted = _store.DeleteAll();

        var query = EntityQueryEnumerator<FSPlayerWalletComponent>();
        while (query.MoveNext(out var mindId, out var wallet))
        {
            wallet.PerkPoints = 0;
            wallet.Credits = StartingCredits;
            MarkWalletDirty(mindId);

        }

        var wiped = new FSPrestigeWipedEvent();
        RaiseLocalEvent(ref wiped);

        Log.Warning($"WipeAllPrestige — deleted {deleted} row(s) and reset all connected players.");
        return deleted;
    }

    public void DumpWallets(IConsoleShell shell)
    {
        var found = false;
        var query = EntityQueryEnumerator<FSPlayerWalletComponent, MindComponent>();
        while (query.MoveNext(out _, out var wallet, out var mind))
        {
            shell.WriteLine($"  {mind.UserId?.ToString() ?? "unknown"} — credits={wallet.Credits}  perkPoints={wallet.PerkPoints}");
            found = true;
        }
        if (!found)
            shell.WriteLine("  (no wallets found)");
    }

    private void OnWalletRequested(WalletRequestEvent req, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        _cachedUsernames[session.UserId] = session.Name;

        if (_mind.TryGetMind(session, out var mindId, out _)
            && TryComp<FSPlayerWalletComponent>(mindId, out var wallet))
        {
            RaiseNetworkEvent(new WalletUpdatedEvent(wallet.Credits, wallet.PerkPoints),
                Filter.SinglePlayer(session));
            return;
        }

        // Lobby players have no mind, so this goes to the database.
        var now = _timing.CurTime;
        if (_lastLobbyRequest.TryGetValue(session.UserId, out var last) && now - last < LobbyRequestInterval)
            return;
        _lastLobbyRequest[session.UserId] = now;

        RaiseNetworkEvent(new WalletUpdatedEvent(0, _store.GetPerkPoints(session.UserId.UserId)),
            Filter.SinglePlayer(session));
    }

    private void MarkWalletDirty(EntityUid mindId)
    {
        _dirtyWallets.Add(mindId);
    }

    private void PushWalletState(EntityUid mindId, FSPlayerWalletComponent wallet)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null)
            return;
        if (!_playerManager.TryGetSessionById(mind.UserId.Value, out var session))
            return;
        RaiseNetworkEvent(new WalletUpdatedEvent(wallet.Credits, wallet.PerkPoints),
            Filter.SinglePlayer(session));
    }
}
