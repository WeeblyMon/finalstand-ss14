using System.IO;
using Content.Shared._FinalStand.Economy;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Microsoft.Data.Sqlite;
using Robust.Shared.Console;
using Robust.Server.Player;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Economy;

public sealed class FSPlayerWalletSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IResourceManager _res = default!;

    private SqliteConnection? _db;
    private readonly Dictionary<NetUserId, string> _cachedUsernames = new();

    public override void Initialize()
    {
        base.Initialize();
        OpenDatabase();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeNetworkEvent<WalletRequestEvent>(OnWalletRequested);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _db?.Dispose();
        _db = null;
    }

    private void OpenDatabase()
    {
        var rootDir = _res.UserData.RootDir;
        if (rootDir == null)
        {
            Log.Warning("[FSWallet] UserData has no root dir — using in-memory DB.");
            _db = new SqliteConnection("Data Source=:memory:");
        }
        else
        {
            var path = Path.Combine(rootDir, "fsprestige.db");
            _db = new SqliteConnection($"Data Source={path}");
        }

        _db.Open();

        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS prestige (
                user_id        TEXT PRIMARY KEY,
                username       TEXT NOT NULL DEFAULT '',
                augment_points INTEGER NOT NULL DEFAULT 0,
                level          INTEGER NOT NULL DEFAULT 1,
                experience     INTEGER NOT NULL DEFAULT 0
            )
            """;
        cmd.ExecuteNonQuery();
        Log.Debug("[FSWallet] Prestige database opened.");
    }

    private int DbGetAugmentPoints(Guid userId)
    {
        using var cmd = _db!.CreateCommand();
        cmd.CommandText = "SELECT augment_points FROM prestige WHERE user_id = $id";
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        var result = cmd.ExecuteScalar();
        return result is long l ? (int)l : 0;
    }

    private void DbUpsert(Guid userId, string username, int augmentPoints)
    {
        using var cmd = _db!.CreateCommand();
        cmd.CommandText = """
            INSERT INTO prestige (user_id, username, augment_points)
            VALUES ($id, $name, $ap)
            ON CONFLICT(user_id) DO UPDATE SET
                username       = excluded.username,
                augment_points = excluded.augment_points
            """;
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        cmd.Parameters.AddWithValue("$name", username);
        cmd.Parameters.AddWithValue("$ap", augmentPoints);
        cmd.ExecuteNonQuery();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        // Mind entities persist across rounds — zero credits but keep augment points.
        var query = EntityQueryEnumerator<FSPlayerWalletComponent>();
        while (query.MoveNext(out var mindId, out var wallet))
        {
            wallet.Credits = 0;
            NotifyClient(mindId, wallet);
        }
        Log.Debug("[FSWallet] Round restart — cleared credits on all wallets.");
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!_mind.TryGetMind(ev.Entity, out var mindId, out var mind) || mind.UserId == null)
            return;
        if (!_playerManager.TryGetSessionById(mind.UserId.Value, out var session))
            return;

        _cachedUsernames[mind.UserId.Value] = session.Name;

        var wallet = EnsureComp<FSPlayerWalletComponent>(mindId);
        wallet.AugmentPoints = DbGetAugmentPoints(mind.UserId.Value.UserId);
        NotifyClient(mindId, wallet);
        Log.Debug($"[FSWallet] Attached {mind.UserId} ({session.Name}) — credits={wallet.Credits} augment={wallet.AugmentPoints}");
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (!_mind.TryGetMind(ev.Entity, out _, out var mind) || mind.UserId == null)
            return;

        var username = ResolveUsername(mind.UserId.Value);

        var query = EntityQueryEnumerator<FSPlayerWalletComponent, MindComponent>();
        while (query.MoveNext(out _, out var wallet, out var m))
        {
            if (m.UserId != mind.UserId)
                continue;
            DbUpsert(mind.UserId.Value.UserId, username, wallet.AugmentPoints);
            Log.Debug($"[FSWallet] Detached {mind.UserId} ({username}) — saved augment={wallet.AugmentPoints}");
            break;
        }
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
        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var mindId, out var mind))
        {
            if (mind.UserId == null) continue;
            if (!_playerManager.TryGetSessionById(mind.UserId.Value, out _)) continue;
            var wallet = EnsureComp<FSPlayerWalletComponent>(mindId);
            wallet.Credits += amount;
            NotifyClient(mindId, wallet);
            count++;
        }
        Log.Info($"[FSWallet] DistributeCredits +{amount} → {count} player(s)");
    }

    public void DistributeAugmentPoints(int amount)
    {
        var count = 0;
        var query = EntityQueryEnumerator<FSPlayerWalletComponent, MindComponent>();
        while (query.MoveNext(out var mindId, out var wallet, out var mind))
        {
            wallet.AugmentPoints += amount;
            NotifyClient(mindId, wallet);
            if (mind.UserId != null && _playerManager.TryGetSessionById(mind.UserId.Value, out var session))
                DbUpsert(mind.UserId.Value.UserId, session.Name, wallet.AugmentPoints);
            count++;
        }
        Log.Info($"[FSWallet] DistributeAugmentPoints +{amount} → {count} player(s)");
    }

    public void GiveCredits(EntityUid mindId, int amount)
    {
        var wallet = EnsureComp<FSPlayerWalletComponent>(mindId);
        wallet.Credits += amount;
        NotifyClient(mindId, wallet);
        Log.Debug($"[FSWallet] GiveCredits +{amount} → mind {mindId}");
    }

    public bool TryDeductCredits(EntityUid mindId, int amount)
    {
        if (!TryComp<FSPlayerWalletComponent>(mindId, out var wallet) || wallet.Credits < amount)
            return false;
        wallet.Credits -= amount;
        NotifyClient(mindId, wallet);
        return true;
    }

    public void SaveAll()
    {
        var count = 0;
        var query = EntityQueryEnumerator<FSPlayerWalletComponent, MindComponent>();
        while (query.MoveNext(out _, out var wallet, out var mind))
        {
            if (mind.UserId == null) continue;
            DbUpsert(mind.UserId.Value.UserId, ResolveUsername(mind.UserId.Value), wallet.AugmentPoints);
            count++;
        }
        Log.Info($"[FSWallet] SaveAll flushed augment points for {count} player(s)");
    }

    public void DumpWallets(IConsoleShell shell)
    {
        var found = false;
        var query = EntityQueryEnumerator<FSPlayerWalletComponent, MindComponent>();
        while (query.MoveNext(out _, out var wallet, out var mind))
        {
            shell.WriteLine($"  {mind.UserId?.ToString() ?? "unknown"} — credits={wallet.Credits}  augment={wallet.AugmentPoints}");
            found = true;
        }
        if (!found)
            shell.WriteLine("  (no wallets found)");
    }

    private void OnWalletRequested(WalletRequestEvent req, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        _cachedUsernames[session.UserId] = session.Name;
        var ap = DbGetAugmentPoints(session.UserId.UserId);
        var credits = 0;
        var query = EntityQueryEnumerator<FSPlayerWalletComponent, MindComponent>();
        while (query.MoveNext(out _, out var wallet, out var mind))
        {
            if (mind.UserId?.UserId == session.UserId.UserId)
            {
                credits = wallet.Credits;
                break;
            }
        }
        RaiseNetworkEvent(new WalletUpdatedEvent(credits, ap), Filter.SinglePlayer(session));
    }

    private void NotifyClient(EntityUid mindId, FSPlayerWalletComponent wallet)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null)
            return;
        if (!_playerManager.TryGetSessionById(mind.UserId.Value, out var session))
            return;
        RaiseNetworkEvent(new WalletUpdatedEvent(wallet.Credits, wallet.AugmentPoints),
            Filter.SinglePlayer(session));
    }
}
