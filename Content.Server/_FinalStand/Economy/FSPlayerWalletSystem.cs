using System.IO;
using Content.Server._FinalStand.Leveling;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Leveling;
using Content.Shared.GameTicking;
using Content.Server.GameTicking;
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
    [Dependency] private readonly FSLevelingSystem _levelingSystem = default!;

    private SqliteConnection? _db;
    private readonly Dictionary<NetUserId, string> _cachedUsernames = new();

    private record struct FullRecord(
        int AugmentPoints,
        int Level,
        int Experience,
        int PrestigeLevel,
        int BuffStoppingPower,
        int BuffBulletStorm);

    public override void Initialize()
    {
        base.Initialize();
        OpenDatabase();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeNetworkEvent<WalletRequestEvent>(OnWalletRequested);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
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

        using var createCmd = _db.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS prestige (
                user_id        TEXT PRIMARY KEY,
                username       TEXT NOT NULL DEFAULT '',
                augment_points INTEGER NOT NULL DEFAULT 0,
                level          INTEGER NOT NULL DEFAULT 1,
                experience     INTEGER NOT NULL DEFAULT 0
            )
            """;
        createCmd.ExecuteNonQuery();

        // sqlite has no IF NOT EXISTS for ALTER TABLE, so we catch duplicate column errors
        string[] newColumns =
        [
            "ALTER TABLE prestige ADD COLUMN prestige_level      INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE prestige ADD COLUMN buff_iron_hide      INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE prestige ADD COLUMN buff_scavenger      INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE prestige ADD COLUMN buff_fast_learner   INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE prestige ADD COLUMN buff_sprinter       INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE prestige ADD COLUMN buff_marksman       INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE prestige ADD COLUMN buff_survivor       INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE prestige ADD COLUMN buff_stopping_power  INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE prestige ADD COLUMN buff_bullet_storm    INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE prestige ADD COLUMN augment_levels_json  TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE prestige ADD COLUMN augment_slots_json   TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE prestige ADD COLUMN augment_loadouts_json TEXT NOT NULL DEFAULT ''",
        ];
        foreach (var ddl in newColumns)
        {
            try
            {
                using var alter = _db.CreateCommand();
                alter.CommandText = ddl;
                alter.ExecuteNonQuery();
            }
            catch (SqliteException e) when (e.Message.Contains("duplicate column")) { }
        }

        Log.Debug("[FSWallet] Prestige database opened.");
    }

    private FullRecord DbGetFullRecord(Guid userId)
    {
        using var cmd = _db!.CreateCommand();
        cmd.CommandText = """
            SELECT augment_points, level, experience, prestige_level,
                   buff_stopping_power, buff_bullet_storm
            FROM prestige WHERE user_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return new FullRecord(0, 1, 0, 0, 0, 0);

        return new FullRecord(
            reader.GetInt32(0),
            Math.Max(1, reader.GetInt32(1)),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5));
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

    private void DbUpsertFull(Guid userId, string username,
        int augmentPoints, int level, int experience, int prestigeLevel,
        int stoppingPower = 0, int bulletStorm = 0)
    {
        using var cmd = _db!.CreateCommand();
        cmd.CommandText = """
            INSERT INTO prestige (
                user_id, username, augment_points,
                level, experience, prestige_level,
                buff_stopping_power, buff_bullet_storm)
            VALUES ($id, $name, $ap, $lvl, $xp, $prestige, $stoppingpower, $bulletstorm)
            ON CONFLICT(user_id) DO UPDATE SET
                username            = excluded.username,
                augment_points      = excluded.augment_points,
                level               = excluded.level,
                experience          = excluded.experience,
                prestige_level      = excluded.prestige_level,
                buff_stopping_power = excluded.buff_stopping_power,
                buff_bullet_storm   = excluded.buff_bullet_storm
            """;
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        cmd.Parameters.AddWithValue("$name", username);
        cmd.Parameters.AddWithValue("$ap", augmentPoints);
        cmd.Parameters.AddWithValue("$lvl", level);
        cmd.Parameters.AddWithValue("$xp", experience);
        cmd.Parameters.AddWithValue("$prestige", prestigeLevel);
        cmd.Parameters.AddWithValue("$stoppingpower", stoppingPower);
        cmd.Parameters.AddWithValue("$bulletstorm", bulletStorm);
        cmd.ExecuteNonQuery();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        var query = EntityQueryEnumerator<FSPlayerWalletComponent>();
        while (query.MoveNext(out var mindId, out var wallet))
        {
            wallet.Credits = 500;
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

        var row = DbGetFullRecord(mind.UserId.Value.UserId);

        var wallet = EnsureComp<FSPlayerWalletComponent>(mindId);
        wallet.AugmentPoints = row.AugmentPoints;
        if (wallet.Credits == 0)
            wallet.Credits = 500;
        NotifyClient(mindId, wallet);

        var lvlComp = EnsureComp<FSPlayerLevelComponent>(mindId);
        lvlComp.Level = row.Level;
        lvlComp.Experience = row.Experience;
        lvlComp.XpToNextLevel = FSLevelingSystem.XpToNextLevel(row.Level);
        lvlComp.PrestigeLevel = row.PrestigeLevel;

        var buffComp = EnsureComp<FSPrestigeBuffsComponent>(mindId);
        buffComp.StoppingPower = row.BuffStoppingPower;
        buffComp.BulletStorm   = row.BuffBulletStorm;

        lvlComp.XpMultiplier = FSLevelingSystem.ComputeXpMultiplier(lvlComp.PrestigeLevel);

        _levelingSystem.SendLevelingUpdate(mindId, lvlComp);

        Log.Debug($"[FSWallet] Attached {mind.UserId} ({session.Name}) — augment={wallet.AugmentPoints} lvl={lvlComp.Level} xp={lvlComp.Experience}");
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!_mind.TryGetMind(ev.Mob, out var mindId, out _))
            return;
        var wallet = EnsureComp<FSPlayerWalletComponent>(mindId);
        if (wallet.Credits == 0)
            wallet.Credits = 500;
        NotifyClient(mindId, wallet);
        Log.Debug($"[FSWallet] SpawnComplete for {ev.Mob} — credits={wallet.Credits}");
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (!_mind.TryGetMind(ev.Entity, out _, out var mind) || mind.UserId == null)
            return;

        var username = ResolveUsername(mind.UserId.Value);

        var query = EntityQueryEnumerator<FSPlayerWalletComponent, MindComponent>();
        while (query.MoveNext(out var mindId, out var wallet, out var m))
        {
            if (m.UserId != mind.UserId) continue;

            TryComp<FSPlayerLevelComponent>(mindId, out var lvl);
            TryComp<FSPrestigeBuffsComponent>(mindId, out var buffs);

            DbUpsertFull(
                mind.UserId.Value.UserId, username,
                wallet.AugmentPoints,
                lvl?.Level ?? 1, lvl?.Experience ?? 0, lvl?.PrestigeLevel ?? 0,
                buffs?.StoppingPower ?? 0, buffs?.BulletStorm ?? 0);

            Log.Debug($"[FSWallet] Detached {mind.UserId} ({username}) — saved augment={wallet.AugmentPoints} lvl={lvl?.Level}");
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

    public void AddAugmentPoints(EntityUid mindId, int amount)
    {
        var wallet = EnsureComp<FSPlayerWalletComponent>(mindId);
        wallet.AugmentPoints += amount;
        NotifyClient(mindId, wallet);
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null) return;
        DbUpsert(mind.UserId.Value.UserId, ResolveUsername(mind.UserId.Value), wallet.AugmentPoints);
    }

    public bool TryDeductAugmentPoints(EntityUid mindId, int cost)
    {
        if (!TryComp<FSPlayerWalletComponent>(mindId, out var wallet) || wallet.AugmentPoints < cost)
            return false;
        wallet.AugmentPoints -= cost;
        NotifyClient(mindId, wallet);
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null) return true;
        DbUpsert(mind.UserId.Value.UserId, ResolveUsername(mind.UserId.Value), wallet.AugmentPoints);
        return true;
    }

    public void SaveLeveling(EntityUid mindId, int level, int experience, int prestigeLevel)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null) return;
        if (!TryComp<FSPlayerWalletComponent>(mindId, out var wallet)) return;

        TryComp<FSPrestigeBuffsComponent>(mindId, out var buffs);
        DbUpsertFull(
            mind.UserId.Value.UserId, ResolveUsername(mind.UserId.Value),
            wallet.AugmentPoints, level, experience, prestigeLevel,
            buffs?.StoppingPower ?? 0, buffs?.BulletStorm ?? 0);
    }

    public void SavePrestigeBuffs(EntityUid mindId, FSPrestigeBuffsComponent buffs)
    {
        if (!TryComp<FSPlayerLevelComponent>(mindId, out var lvl)) return;
        SaveLeveling(mindId, lvl.Level, lvl.Experience, lvl.PrestigeLevel);
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
        while (query.MoveNext(out var mindId, out var wallet, out var mind))
        {
            if (mind.UserId == null) continue;
            TryComp<FSPlayerLevelComponent>(mindId, out var lvl);
            TryComp<FSPrestigeBuffsComponent>(mindId, out var buffs);
            DbUpsertFull(
                mind.UserId.Value.UserId, ResolveUsername(mind.UserId.Value),
                wallet.AugmentPoints,
                lvl?.Level ?? 1, lvl?.Experience ?? 0, lvl?.PrestigeLevel ?? 0,
                buffs?.StoppingPower ?? 0, buffs?.BulletStorm ?? 0);
            count++;
        }
        Log.Info($"[FSWallet] SaveAll flushed {count} player(s)");
    }

    public int GetStoredAugmentPoints(Guid userId) => DbGetAugmentPoints(userId);

    public void GiveAugmentPoints(ICommonSession session, int amount)
    {
        var query = EntityQueryEnumerator<FSPlayerWalletComponent, MindComponent>();
        while (query.MoveNext(out var mindId, out var wallet, out var mind))
        {
            if (mind.UserId != session.UserId) continue;
            wallet.AugmentPoints = Math.Max(0, wallet.AugmentPoints + amount);
            NotifyClient(mindId, wallet);
            DbUpsert(session.UserId.UserId, session.Name, wallet.AugmentPoints);
            return;
        }

        var current = DbGetAugmentPoints(session.UserId.UserId);
        DbUpsert(session.UserId.UserId, session.Name, Math.Max(0, current + amount));
    }

    public (string LevelsJson, string SlotsJson, string LoadoutsJson) LoadAugmentData(Guid userId)
    {
        using var cmd = _db!.CreateCommand();
        cmd.CommandText = """
            SELECT augment_levels_json, augment_slots_json, augment_loadouts_json
            FROM prestige WHERE user_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return ("", "", "");
        return (
            reader.IsDBNull(0) ? "" : reader.GetString(0),
            reader.IsDBNull(1) ? "" : reader.GetString(1),
            reader.IsDBNull(2) ? "" : reader.GetString(2));
    }

    public void SaveAugmentData(EntityUid mindId, string levelsJson, string slotsJson, string loadoutsJson)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null) return;
        var userGuid = mind.UserId.Value.UserId;
        DbSaveAugmentJson(userGuid, levelsJson, slotsJson, loadoutsJson);
    }

    public void SaveAugmentDataByUser(Guid userId, string levelsJson, string slotsJson, string loadoutsJson)
        => DbSaveAugmentJson(userId, levelsJson, slotsJson, loadoutsJson);

    private void DbSaveAugmentJson(Guid userGuid, string levelsJson, string slotsJson, string loadoutsJson)
    {
        using var cmd = _db!.CreateCommand();
        cmd.CommandText = """
            INSERT INTO prestige (user_id, username, augment_levels_json, augment_slots_json, augment_loadouts_json)
            VALUES ($id, '', $levels, $slots, $loadouts)
            ON CONFLICT(user_id) DO UPDATE SET
                augment_levels_json   = excluded.augment_levels_json,
                augment_slots_json    = excluded.augment_slots_json,
                augment_loadouts_json = excluded.augment_loadouts_json
            """;
        cmd.Parameters.AddWithValue("$id", userGuid.ToString());
        cmd.Parameters.AddWithValue("$levels", levelsJson);
        cmd.Parameters.AddWithValue("$slots", slotsJson);
        cmd.Parameters.AddWithValue("$loadouts", loadoutsJson);
        cmd.ExecuteNonQuery();
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
