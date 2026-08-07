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

namespace Content.Server._FinalStand.Economy;

public sealed class FSPlayerWalletSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly FSPlayerDataStore _store = default!;

    private readonly Dictionary<NetUserId, string> _cachedUsernames = new();

    // Credits change on every damaging hit (money-on-hit), every kill and every assist.
    // Pushing a network event per change floods one client with hundreds of messages a wave,
    // so the hot path marks the wallet dirty and a flush coalesces them.
    private readonly HashSet<EntityUid> _dirtyWallets = new();
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

    public override void Shutdown()
    {
        base.Shutdown();
        SaveAll();
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
        SaveAll();
        _dirtyWallets.Clear();

        // Credits are round-scoped. Clearing Loaded makes the next spawn re-read the row, which
        // is what carries perk points and levels into the new round.
        var query = EntityQueryEnumerator<FSPlayerWalletComponent>();
        while (query.MoveNext(out _, out var wallet))
        {
            wallet.Credits = 0;
            wallet.Loaded = false;
        }
        Log.Debug("[FSWallet] Round restart — saved all players, cleared credits.");
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

        var wallet = EnsureComp<FSPlayerWalletComponent>(mindId);

        // A second spawn for the same mind must not re-read the row. Everything earned since
        // the first spawn lives on the component, and the row is only as fresh as the last
        // write-through.
        if (wallet.Loaded)
        {
            MarkWalletDirty(mindId);
            return;
        }

        var row = _store.GetFullRecord(ev.Player.UserId.UserId);

        wallet.PerkPoints = row.PerkPoints;
        // Added, not assigned: another handler on this same event may have already paid this
        // player (late-join catch-up), and handler order between systems is not defined.
        wallet.Credits += StartingCredits;
        wallet.Loaded = true;
        MarkWalletDirty(mindId);

        Log.Info($"[FSWallet] Loaded {ev.Player.Name} — perkPoints={wallet.PerkPoints}");
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (!_mind.TryGetMind(ev.Entity, out var mindId, out var mind) || mind.UserId == null)
            return;

        SaveMind(mindId, mind.UserId.Value);
    }

    // Writes one mind's persistent state to its user's row. Every save path goes through here,
    // so the mind that owns the state is always the mind that gets written.
    private void SaveMind(EntityUid mindId, NetUserId userId)
    {
        if (!TryComp<FSPlayerWalletComponent>(mindId, out var wallet))
            return;

        // Only the wallet's own column. Leveling writes its columns from FSLevelingSystem.
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

            var wallet = EnsureComp<FSPlayerWalletComponent>(mindId);
            wallet.Credits += amount;
            MarkWalletDirty(mindId);
            count++;
        }
        Log.Info($"[FSWallet] DistributeCredits +{amount} → {count} player(s)");
    }

    public void DistributePerkPoints(int amount)
    {
        var count = 0;
        foreach (var session in _playerManager.Sessions)
        {
            if (!_mind.TryGetMind(session, out var mindId, out _))
                continue;

            var wallet = EnsureComp<FSPlayerWalletComponent>(mindId);
            wallet.PerkPoints += amount;
            MarkWalletDirty(mindId);
            _store.UpsertPerkPoints(session.UserId.UserId, session.Name, wallet.PerkPoints);
            count++;
        }
        Log.Info($"[FSWallet] DistributePerkPoints +{amount} → {count} player(s)");
    }

    public void AddPerkPoints(EntityUid mindId, int amount)
    {
        var wallet = EnsureComp<FSPlayerWalletComponent>(mindId);
        wallet.PerkPoints += amount;
        MarkWalletDirty(mindId);
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null) return;
        _store.UpsertPerkPoints(mind.UserId.Value.UserId, ResolveUsername(mind.UserId.Value), wallet.PerkPoints);
    }

    public bool TryDeductPerkPoints(EntityUid mindId, int cost)
    {
        if (!TryComp<FSPlayerWalletComponent>(mindId, out var wallet) || wallet.PerkPoints < cost)
            return false;
        wallet.PerkPoints -= cost;
        MarkWalletDirty(mindId);
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null) return true;
        _store.UpsertPerkPoints(mind.UserId.Value.UserId, ResolveUsername(mind.UserId.Value), wallet.PerkPoints);
        return true;
    }


    public void GiveCredits(EntityUid mindId, int amount)
    {
        var wallet = EnsureComp<FSPlayerWalletComponent>(mindId);
        wallet.Credits += amount;
        MarkWalletDirty(mindId);
    }

    public bool TryDeductCredits(EntityUid mindId, int amount)
    {
        if (!TryComp<FSPlayerWalletComponent>(mindId, out var wallet) || wallet.Credits < amount)
            return false;
        wallet.Credits -= amount;
        MarkWalletDirty(mindId);
        return true;
    }

    // Flushes every connected player. Players who left were already saved on detach.
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

        Log.Info($"[FSWallet] SaveAll flushed {count} player(s)");
    }

    /// <summary>
    /// Perk points for a user. The wallet component is authoritative whenever the player has a
    /// mind; the row is only read for a lobby user who has not spawned yet. Resolving it here
    /// rather than at each call site means a caller cannot accidentally read a stale row for a
    /// player who is in the round.
    /// </summary>
    public int GetStoredPerkPoints(Guid userId)
    {
        if (_playerManager.TryGetSessionById(new NetUserId(userId), out var session)
            && _mind.TryGetMind(session, out var mindId, out _)
            && TryComp<FSPlayerWalletComponent>(mindId, out var wallet))
        {
            return wallet.PerkPoints;
        }

        return _store.GetPerkPoints(userId);
    }

    // Works both in-round and from the lobby. A player in the lobby has no mind, so the row is
    // the only copy of their perk points; a player in the round has a wallet that must stay in
    // step with it. This is the single bridge between those two states.
    public void GivePerkPoints(ICommonSession session, int amount)
    {
        if (_mind.TryGetMind(session, out var mindId, out _)
            && TryComp<FSPlayerWalletComponent>(mindId, out var wallet))
        {
            wallet.PerkPoints = Math.Max(0, wallet.PerkPoints + amount);
            MarkWalletDirty(mindId);
            _store.UpsertPerkPoints(session.UserId.UserId, session.Name, wallet.PerkPoints);
            return;
        }

        var current = _store.GetPerkPoints(session.UserId.UserId);
        _store.UpsertPerkPoints(session.UserId.UserId, session.Name, Math.Max(0, current + amount));
    }

    public int WipeAllPrestige()
    {
        var deleted = _store.DeleteAll();

        // Anchored on the wallet, not on all three components — a player without a level
        // component would otherwise keep their perk points through a wipe.
        var query = EntityQueryEnumerator<FSPlayerWalletComponent>();
        while (query.MoveNext(out var mindId, out var wallet))
        {
            wallet.PerkPoints = 0;
            wallet.Credits = StartingCredits;
            MarkWalletDirty(mindId);

        }

        var wiped = new FSPrestigeWipedEvent();
        RaiseLocalEvent(ref wiped);

        Log.Warning($"[FSWallet] WipeAllPrestige — deleted {deleted} row(s) and reset all connected players.");
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

    // Client-triggered, so it must not touch the database — a client can send this in a loop.
    // The live wallet is authoritative in-round; the row is only read for a lobby client that
    // has no mind yet.
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

        RaiseNetworkEvent(new WalletUpdatedEvent(0, _store.GetPerkPoints(session.UserId.UserId)),
            Filter.SinglePlayer(session));
    }

    /// <summary>Queues a balance update. Several changes in the same tick send one message.</summary>
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
