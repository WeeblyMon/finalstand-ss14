using System.IO;
using System.Text.Json;
using Content.Server._FinalStand.Perks;
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
        int PerkPoints,
        int Level,
        int Experience,
        int PrestigeLevel,
        string PrestigeBuffsJson);

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
        SaveAll();
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
                experience     INTEGER NOT NULL DEFAULT 0,
                prestige_level INTEGER NOT NULL DEFAULT 0,
                augment_data   TEXT NOT NULL DEFAULT '{}',
                prestige_buffs TEXT NOT NULL DEFAULT '{}'
            )
            """;
        createCmd.ExecuteNonQuery();

        TryMigrateOldSchema();

        Log.Debug("[FSWallet] Prestige database opened.");
    }

    private void TryMigrateOldSchema()
    {
        // Detect old schema by looking for the buff_iron_hide column
        var isOld = false;
        using (var pragma = _db!.CreateCommand())
        {
            pragma.CommandText = "PRAGMA table_info(prestige)";
            using var r = pragma.ExecuteReader();
            while (r.Read())
            {
                if (r.GetString(1) == "buff_iron_hide")
                {
                    isOld = true;
                    break;
                }
            }
        }
        if (!isOld) return;

        Log.Info("[FSWallet] Old schema detected — migrating to new schema...");

        // Read all old rows in C# so we can handle null/invalid JSON safely
        var rows = new List<(string UserId, string Username, int Ap, int Level, int Xp, int Prestige,
            string LevelsJson, string SlotsJson, string LoadoutsJson, int StoppingPower, int BulletStorm)>();

        using (var sel = _db!.CreateCommand())
        {
            sel.CommandText = """
                SELECT user_id, username, augment_points, level, experience, prestige_level,
                       augment_levels_json, augment_slots_json, augment_loadouts_json,
                       buff_stopping_power, buff_bullet_storm
                FROM prestige
                """;
            using var r = sel.ExecuteReader();
            while (r.Read())
            {
                rows.Add((
                    r.GetString(0),
                    r.IsDBNull(1) ? "" : r.GetString(1),
                    r.IsDBNull(2) ? 0  : r.GetInt32(2),
                    r.IsDBNull(3) ? 1  : Math.Max(1, r.GetInt32(3)),
                    r.IsDBNull(4) ? 0  : r.GetInt32(4),
                    r.IsDBNull(5) ? 0  : r.GetInt32(5),
                    r.IsDBNull(6) ? "" : r.GetString(6),
                    r.IsDBNull(7) ? "" : r.GetString(7),
                    r.IsDBNull(8) ? "" : r.GetString(8),
                    r.IsDBNull(9) ? 0  : r.GetInt32(9),
                    r.IsDBNull(10)? 0  : r.GetInt32(10)));
            }
        }

        using (var createNew = _db!.CreateCommand())
        {
            createNew.CommandText = """
                CREATE TABLE prestige_new (
                    user_id        TEXT PRIMARY KEY,
                    username       TEXT NOT NULL DEFAULT '',
                    augment_points INTEGER NOT NULL DEFAULT 0,
                    level          INTEGER NOT NULL DEFAULT 1,
                    experience     INTEGER NOT NULL DEFAULT 0,
                    prestige_level INTEGER NOT NULL DEFAULT 0,
                    augment_data   TEXT NOT NULL DEFAULT '{}',
                    prestige_buffs TEXT NOT NULL DEFAULT '{}'
                )
                """;
            createNew.ExecuteNonQuery();
        }

        using (var tx = _db!.BeginTransaction())
        {
            foreach (var row in rows)
            {
                var augData = BuildAugmentDataJson(row.LevelsJson, row.SlotsJson, row.LoadoutsJson);

                var buffDict = new Dictionary<string, int>();
                if (row.StoppingPower > 0) buffDict["StoppingPower"] = row.StoppingPower;
                if (row.BulletStorm   > 0) buffDict["BulletStorm"]   = row.BulletStorm;
                var buffJson = JsonSerializer.Serialize(buffDict);

                using var ins = _db!.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = """
                    INSERT INTO prestige_new (user_id, username, augment_points, level, experience,
                                             prestige_level, augment_data, prestige_buffs)
                    VALUES ($id, $name, $ap, $lvl, $xp, $prestige, $data, $buffs)
                    """;
                ins.Parameters.AddWithValue("$id",      row.UserId);
                ins.Parameters.AddWithValue("$name",    row.Username);
                ins.Parameters.AddWithValue("$ap",      row.Ap);
                ins.Parameters.AddWithValue("$lvl",     row.Level);
                ins.Parameters.AddWithValue("$xp",      row.Xp);
                ins.Parameters.AddWithValue("$prestige",row.Prestige);
                ins.Parameters.AddWithValue("$data",    augData);
                ins.Parameters.AddWithValue("$buffs",   buffJson);
                ins.ExecuteNonQuery();
            }
            tx.Commit();
        }

        using (var drop = _db!.CreateCommand())
        {
            drop.CommandText = "DROP TABLE prestige";
            drop.ExecuteNonQuery();
        }
        using (var rename = _db!.CreateCommand())
        {
            rename.CommandText = "ALTER TABLE prestige_new RENAME TO prestige";
            rename.ExecuteNonQuery();
        }

        Log.Info($"[FSWallet] Migration complete — {rows.Count} row(s) moved to new schema.");
    }

    private static string BuildAugmentDataJson(string levelsJson, string slotsJson, string loadoutsJson)
    {
        var levels   = SafeParseJson(levelsJson,   "{}");
        var slots    = SafeParseJson(slotsJson,    "[]");
        var loadouts = SafeParseJson(loadoutsJson, "[]");
        return $"{{\"levels\":{levels},\"slots\":{slots},\"loadouts\":{loadouts}}}";
    }

    private static string SafeParseJson(string json, string fallback)
    {
        if (string.IsNullOrWhiteSpace(json)) return fallback;
        try { JsonDocument.Parse(json); return json; }
        catch { return fallback; }
    }

    private static string SerializePrestigeBuffs(FSPrestigeBuffsComponent? buffs)
    {
        if (buffs == null) return "{}";
        var dict = new Dictionary<string, int>();
        if (buffs.StoppingPower > 0) dict["StoppingPower"] = buffs.StoppingPower;
        if (buffs.BulletStorm   > 0) dict["BulletStorm"]   = buffs.BulletStorm;
        return JsonSerializer.Serialize(dict);
    }

    private FullRecord DbGetFullRecord(Guid userId)
    {
        using var cmd = _db!.CreateCommand();
        cmd.CommandText = """
            SELECT augment_points, level, experience, prestige_level, prestige_buffs
            FROM prestige WHERE user_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return new FullRecord(0, 1, 0, 0, "{}");

        return new FullRecord(
            reader.GetInt32(0),
            Math.Max(1, reader.GetInt32(1)),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.IsDBNull(4) ? "{}" : reader.GetString(4));
    }

    private int DbGetPerkPoints(Guid userId)
    {
        using var cmd = _db!.CreateCommand();
        cmd.CommandText = "SELECT augment_points FROM prestige WHERE user_id = $id";
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        var result = cmd.ExecuteScalar();
        return result is long l ? (int)l : 0;
    }

    private void DbUpsert(Guid userId, string username, int PerkPoints)
    {
        using var cmd = _db!.CreateCommand();
        cmd.CommandText = """
            INSERT INTO prestige (user_id, username, augment_points)
            VALUES ($id, $name, $ap)
            ON CONFLICT(user_id) DO UPDATE SET
                username       = excluded.username,
                augment_points = excluded.augment_points
            """;
        cmd.Parameters.AddWithValue("$id",   userId.ToString());
        cmd.Parameters.AddWithValue("$name", username);
        cmd.Parameters.AddWithValue("$ap",   PerkPoints);
        cmd.ExecuteNonQuery();
    }

    private void DbUpsertFull(Guid userId, string username,
        int PerkPoints, int level, int experience, int prestigeLevel,
        string prestigeBuffsJson = "{}")
    {
        using var cmd = _db!.CreateCommand();
        cmd.CommandText = """
            INSERT INTO prestige (
                user_id, username, augment_points,
                level, experience, prestige_level, prestige_buffs)
            VALUES ($id, $name, $ap, $lvl, $xp, $prestige, $buffs)
            ON CONFLICT(user_id) DO UPDATE SET
                username       = excluded.username,
                augment_points = excluded.augment_points,
                level          = excluded.level,
                experience     = excluded.experience,
                prestige_level = excluded.prestige_level,
                prestige_buffs = excluded.prestige_buffs
            """;
        cmd.Parameters.AddWithValue("$id",      userId.ToString());
        cmd.Parameters.AddWithValue("$name",    username);
        cmd.Parameters.AddWithValue("$ap",      PerkPoints);
        cmd.Parameters.AddWithValue("$lvl",     level);
        cmd.Parameters.AddWithValue("$xp",      experience);
        cmd.Parameters.AddWithValue("$prestige",prestigeLevel);
        cmd.Parameters.AddWithValue("$buffs",   prestigeBuffsJson);
        cmd.ExecuteNonQuery();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        SaveAll();
        var query = EntityQueryEnumerator<FSPlayerWalletComponent>();
        while (query.MoveNext(out var mindId, out var wallet))
        {
            wallet.Credits = 500;
            NotifyClient(mindId, wallet);
        }
        Log.Debug("[FSWallet] Round restart — saved all players, cleared credits.");
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!_mind.TryGetMind(ev.Player, out var mindId, out var mind) || mind.UserId == null)
            return;

        _cachedUsernames[mind.UserId.Value] = ev.Player.Name;

        if (TryComp<FSPlayerLevelComponent>(mindId, out var lvl))
            _levelingSystem.SendLevelingUpdate(mindId, lvl);
        if (TryComp<FSPlayerWalletComponent>(mindId, out var wallet))
            NotifyClient(mindId, wallet);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!_mind.TryGetMind(ev.Mob, out var mindId, out _))
            return;

        _cachedUsernames[ev.Player.UserId] = ev.Player.Name;

        var row = DbGetFullRecord(ev.Player.UserId.UserId);

        var wallet = EnsureComp<FSPlayerWalletComponent>(mindId);
        wallet.PerkPoints = row.PerkPoints;
        if (wallet.Credits == 0)
            wallet.Credits = 500;
        NotifyClient(mindId, wallet);

        var lvlComp = EnsureComp<FSPlayerLevelComponent>(mindId);
        lvlComp.Level          = row.Level;
        lvlComp.Experience     = row.Experience;
        lvlComp.XpToNextLevel  = FSLevelingSystem.XpToNextLevel(row.Level);
        lvlComp.PrestigeLevel  = row.PrestigeLevel;
        lvlComp.XpMultiplier   = FSLevelingSystem.ComputeXpMultiplier(lvlComp.PrestigeLevel);

        var buffComp = EnsureComp<FSPrestigeBuffsComponent>(mindId);
        if (!string.IsNullOrEmpty(row.PrestigeBuffsJson) && row.PrestigeBuffsJson != "{}")
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(row.PrestigeBuffsJson);
                if (dict != null)
                {
                    buffComp.StoppingPower = dict.GetValueOrDefault("StoppingPower");
                    buffComp.BulletStorm   = dict.GetValueOrDefault("BulletStorm");
                }
            }
            catch { }
        }

        _levelingSystem.SendLevelingUpdate(mindId, lvlComp);

        Log.Info($"[FSWallet] SpawnComplete {ev.Player.Name} — augment={wallet.PerkPoints} lvl={lvlComp.Level} xp={lvlComp.Experience}");
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
                wallet.PerkPoints,
                lvl?.Level ?? 1, lvl?.Experience ?? 0, lvl?.PrestigeLevel ?? 0,
                SerializePrestigeBuffs(buffs));

            Log.Debug($"[FSWallet] Detached {mind.UserId} ({username}) — saved augment={wallet.PerkPoints} lvl={lvl?.Level}");
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
        foreach (var session in _playerManager.Sessions)
        {
            if (!_mind.TryGetMind(session, out var mindId, out _))
                continue;

            var wallet = EnsureComp<FSPlayerWalletComponent>(mindId);
            wallet.Credits += amount;
            NotifyClient(mindId, wallet);
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
            NotifyClient(mindId, wallet);
            DbUpsert(session.UserId.UserId, session.Name, wallet.PerkPoints);
            count++;
        }
        Log.Info($"[FSWallet] DistributePerkPoints +{amount} → {count} player(s)");
    }

    public void AddPerkPoints(EntityUid mindId, int amount)
    {
        var wallet = EnsureComp<FSPlayerWalletComponent>(mindId);
        wallet.PerkPoints += amount;
        NotifyClient(mindId, wallet);
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null) return;
        DbUpsert(mind.UserId.Value.UserId, ResolveUsername(mind.UserId.Value), wallet.PerkPoints);
    }

    public bool TryDeductPerkPoints(EntityUid mindId, int cost)
    {
        if (!TryComp<FSPlayerWalletComponent>(mindId, out var wallet) || wallet.PerkPoints < cost)
            return false;
        wallet.PerkPoints -= cost;
        NotifyClient(mindId, wallet);
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null) return true;
        DbUpsert(mind.UserId.Value.UserId, ResolveUsername(mind.UserId.Value), wallet.PerkPoints);
        return true;
    }

    public void SaveLeveling(EntityUid mindId, int level, int experience, int prestigeLevel)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null)
        {
            Log.Warning($"[FSWallet] SaveLeveling: null UserId for mind {mindId} — save skipped (lv{level} xp{experience})");
            return;
        }
        if (!TryComp<FSPlayerWalletComponent>(mindId, out var wallet)) return;

        TryComp<FSPrestigeBuffsComponent>(mindId, out var buffs);
        DbUpsertFull(
            mind.UserId.Value.UserId, ResolveUsername(mind.UserId.Value),
            wallet.PerkPoints, level, experience, prestigeLevel,
            SerializePrestigeBuffs(buffs));
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
                wallet.PerkPoints,
                lvl?.Level ?? 1, lvl?.Experience ?? 0, lvl?.PrestigeLevel ?? 0,
                SerializePrestigeBuffs(buffs));
            count++;
        }
        Log.Info($"[FSWallet] SaveAll flushed {count} player(s)");
    }

    public int GetStoredPerkPoints(Guid userId) => DbGetPerkPoints(userId);

    public void GivePerkPoints(ICommonSession session, int amount)
    {
        var query = EntityQueryEnumerator<FSPlayerWalletComponent, MindComponent>();
        while (query.MoveNext(out var mindId, out var wallet, out var mind))
        {
            if (mind.UserId != session.UserId) continue;
            wallet.PerkPoints = Math.Max(0, wallet.PerkPoints + amount);
            NotifyClient(mindId, wallet);
            DbUpsert(session.UserId.UserId, session.Name, wallet.PerkPoints);
            return;
        }

        var current = DbGetPerkPoints(session.UserId.UserId);
        DbUpsert(session.UserId.UserId, session.Name, Math.Max(0, current + amount));
    }

    public (string LevelsJson, string SlotsJson, string LoadoutsJson) LoadAugmentData(Guid userId)
    {
        using var cmd = _db!.CreateCommand();
        cmd.CommandText = "SELECT augment_data FROM prestige WHERE user_id = $id";
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(0))
            return ("", "", "");

        var json = reader.GetString(0);
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return ("", "", "");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var levels   = root.TryGetProperty("levels",   out var lp) ? lp.GetRawText() : "";
            var slots    = root.TryGetProperty("slots",    out var sp) ? sp.GetRawText() : "";
            var loadouts = root.TryGetProperty("loadouts", out var lo) ? lo.GetRawText() : "";
            return (levels, slots, loadouts);
        }
        catch
        {
            return ("", "", "");
        }
    }

    public void SaveAugmentData(EntityUid mindId, string levelsJson, string slotsJson, string loadoutsJson)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null) return;
        DbSaveAugmentJson(mind.UserId.Value.UserId, levelsJson, slotsJson, loadoutsJson);
    }

    public void SaveAugmentDataByUser(Guid userId, string levelsJson, string slotsJson, string loadoutsJson)
        => DbSaveAugmentJson(userId, levelsJson, slotsJson, loadoutsJson);

    private void DbSaveAugmentJson(Guid userGuid, string levelsJson, string slotsJson, string loadoutsJson)
    {
        var mergedJson = BuildAugmentDataJson(levelsJson, slotsJson, loadoutsJson);

        using var cmd = _db!.CreateCommand();
        cmd.CommandText = """
            INSERT INTO prestige (user_id, augment_data)
            VALUES ($id, $data)
            ON CONFLICT(user_id) DO UPDATE SET
                augment_data = excluded.augment_data
            """;
        cmd.Parameters.AddWithValue("$id",   userGuid.ToString());
        cmd.Parameters.AddWithValue("$data", mergedJson);
        cmd.ExecuteNonQuery();
    }

    public int WipeAllPrestige()
    {
        using var cmd = _db!.CreateCommand();
        cmd.CommandText = "DELETE FROM prestige";
        var deleted = cmd.ExecuteNonQuery();

        var query = EntityQueryEnumerator<FSPlayerWalletComponent, FSPlayerLevelComponent, MindComponent>();
        while (query.MoveNext(out var mindId, out var wallet, out var lvl, out _))
        {
            wallet.PerkPoints = 0;
            wallet.Credits = 500;
            lvl.Level         = 1;
            lvl.Experience    = 0;
            lvl.XpToNextLevel = FSLevelingSystem.XpToNextLevel(1);
            lvl.PrestigeLevel = 0;
            lvl.XpMultiplier  = FSLevelingSystem.ComputeXpMultiplier(0);
            NotifyClient(mindId, wallet);
            _levelingSystem.SendLevelingUpdate(mindId, lvl);
            if (TryComp<FSPerkLevelsComponent>(mindId, out var augs))
            {
                augs.Levels.Clear();
                Array.Fill(augs.Slots, string.Empty);
                foreach (var loadout in augs.Loadouts)
                    Array.Fill(loadout, string.Empty);
            }
        }

        Log.Warning($"[FSWallet] WipeAllPrestige — deleted {deleted} row(s) and reset all connected players.");
        return deleted;
    }

    public void DumpWallets(IConsoleShell shell)
    {
        var found = false;
        var query = EntityQueryEnumerator<FSPlayerWalletComponent, MindComponent>();
        while (query.MoveNext(out _, out var wallet, out var mind))
        {
            shell.WriteLine($"  {mind.UserId?.ToString() ?? "unknown"} — credits={wallet.Credits}  augment={wallet.PerkPoints}");
            found = true;
        }
        if (!found)
            shell.WriteLine("  (no wallets found)");
    }

    private void OnWalletRequested(WalletRequestEvent req, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        _cachedUsernames[session.UserId] = session.Name;
        var ap = DbGetPerkPoints(session.UserId.UserId);
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
        RaiseNetworkEvent(new WalletUpdatedEvent(wallet.Credits, wallet.PerkPoints),
            Filter.SinglePlayer(session));
    }
}
