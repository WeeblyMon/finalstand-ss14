// Owns the prestige database: connection, schema, migration, and typed row access.
// This is the only class in _FinalStand that touches SQL. It holds no game state and depends on no
// game system, so nothing can create a cycle back into it.
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Robust.Shared.ContentPack;

namespace Content.Server._FinalStand.Economy;

public sealed class FSPlayerDataStore : EntitySystem
{
    [Dependency] private readonly IResourceManager _res = default!;

    private SqliteConnection? _db;
    private SqliteTransaction? _activeTx;

    public override void Initialize()
    {
        base.Initialize();
        OpenDatabase();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _db?.Dispose();
        _db = null;
    }

    /// <summary>True once the database is open. False means this session cannot persist.</summary>
    public bool IsOpen => _db != null;

    /// <summary>
    /// Runs several writes in one transaction. Without this each write is its own fsync.
    /// Rolls back and logs if the body throws; the caller is never handed the exception.
    /// </summary>
    public void RunBatch(string what, Action body)
    {
        if (!DbReady(what))
            return;

        using var tx = _db!.BeginTransaction();
        _activeTx = tx;
        try
        {
            body();
            tx.Commit();
        }
        catch (Exception e)
        {
            Log.Error($"[FSDataStore] {what} failed and was rolled back: {e.Message}");
        }
        finally
        {
            _activeTx = null;
        }
    }

    /// <summary>
    /// Writes only the leveling columns. Kept separate from the perk-point upsert so the wallet
    /// and the leveling system each own their own columns of the shared row.
    /// </summary>
    public void UpsertLeveling(Guid userId, int level, int experience, int prestigeLevel, string prestigeBuffsJson)
    {
        if (!DbReady("leveling save")) return;

        using var cmd = DbCommand();
        cmd.CommandText = """
            INSERT INTO prestige (user_id, level, experience, prestige_level, prestige_buffs)
            VALUES ($id, $lvl, $xp, $prestige, $buffs)
            ON CONFLICT(user_id) DO UPDATE SET
                level          = excluded.level,
                experience     = excluded.experience,
                prestige_level = excluded.prestige_level,
                prestige_buffs = excluded.prestige_buffs
            """;
        cmd.Parameters.AddWithValue("$id",      userId.ToString());
        cmd.Parameters.AddWithValue("$lvl",     level);
        cmd.Parameters.AddWithValue("$xp",      experience);
        cmd.Parameters.AddWithValue("$prestige",prestigeLevel);
        cmd.Parameters.AddWithValue("$buffs",   prestigeBuffsJson);
        TryExecute(cmd, $"leveling save for {userId}");
    }

    /// <summary>Deletes every row. Returns rows removed, or 0 on failure.</summary>
    public int DeleteAll()
    {
        if (!DbReady("prestige wipe"))
            return 0;

        using var cmd = DbCommand();
        cmd.CommandText = "DELETE FROM prestige";
        var deleted = TryExecuteCounted(cmd, "prestige wipe");
        return deleted < 0 ? 0 : deleted;
    }

    // Persistence must never take the round down with it. The component holds the live value, so a
    // failed write costs at most one save and a failed read falls back to a safe default. Both are
    // logged at Error — silence here is how progression disappears without anyone noticing.
    private bool TryExecute(SqliteCommand cmd, string what)
        => TryExecuteCounted(cmd, what) >= 0;

    /// <summary>Rows affected, or -1 if the statement failed.</summary>
    private int TryExecuteCounted(SqliteCommand cmd, string what)
    {
        try
        {
            return cmd.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            Log.Error($"[FSWallet] {what} failed: {e.Message}");
            return -1;
        }
    }

    private T TryRead<T>(string what, Func<T> read, T fallback)
    {
        if (_db == null)
        {
            Log.Error($"[FSWallet] {what}: database is not open");
            return fallback;
        }

        try
        {
            return read();
        }
        catch (Exception e)
        {
            Log.Error($"[FSWallet] {what} failed: {e.Message}");
            return fallback;
        }
    }

    private bool DbReady(string what)
    {
        if (_db != null)
            return true;

        Log.Error($"[FSWallet] {what}: database is not open, change kept in memory only");
        return false;
    }

    private SqliteCommand DbCommand()
    {
        var cmd = _db!.CreateCommand();
        cmd.Transaction = _activeTx;
        return cmd;
    }

    public record struct FullRecord(
        int PerkPoints,
        int Level,
        int Experience,
        int PrestigeLevel,
        string PrestigeBuffsJson);

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

        try
        {
            _db.Open();
            InitSchema(_db);
        }
        catch (Exception e)
        {
            // Without a connection the wallet still runs: credits are round-scoped anyway and perk
            // points stay live on the component. Only persistence is lost, and loudly.
            Log.Error($"[FSWallet] Could not open the prestige database — progression will NOT be saved this session: {e}");
            _db?.Dispose();
            _db = null;
        }
    }

    private void InitSchema(SqliteConnection db)
    {
        // Every perk-point change writes through immediately. On the default rollback journal
        // each of those is its own fsync on the game thread; WAL makes them cheap.
        foreach (var pragmaText in new[] { "PRAGMA journal_mode=WAL", "PRAGMA synchronous=NORMAL" })
        {
            using var pragma = db.CreateCommand();
            pragma.CommandText = pragmaText;
            pragma.ExecuteNonQuery();
        }

        using var createCmd = db.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS prestige (
                user_id        TEXT PRIMARY KEY,
                username       TEXT NOT NULL DEFAULT '',
                perk_points INTEGER NOT NULL DEFAULT 0,
                level          INTEGER NOT NULL DEFAULT 1,
                experience     INTEGER NOT NULL DEFAULT 0,
                prestige_level INTEGER NOT NULL DEFAULT 0,
                perk_loadout   TEXT NOT NULL DEFAULT '{}',
                prestige_buffs TEXT NOT NULL DEFAULT '{}'
            )
            """;
        createCmd.ExecuteNonQuery();

        TryMigrateOldSchema();
        TryRenameLegacyColumns(db);

        Log.Debug("[FSWallet] Prestige database opened.");
    }

    // The perks rename left two columns named after the old "augment" wording. SQLite has
    // supported ALTER TABLE RENAME COLUMN since 3.25, so this is a single statement per column and
    // is skipped once the new names are present.
    private void TryRenameLegacyColumns(SqliteConnection db)
    {
        var columns = new List<string>();
        using (var pragma = db.CreateCommand())
        {
            pragma.CommandText = "PRAGMA table_info(prestige)";
            using var r = pragma.ExecuteReader();
            while (r.Read())
                columns.Add(r.GetString(1));
        }

        foreach (var (oldName, newName) in new[] { ("augment_points", "perk_points"), ("augment_data", "perk_loadout") })
        {
            if (!columns.Contains(oldName) || columns.Contains(newName))
                continue;

            using var cmd = db.CreateCommand();
            cmd.CommandText = $"ALTER TABLE prestige RENAME COLUMN {oldName} TO {newName}";
            cmd.ExecuteNonQuery();
            Log.Info($"[FSDataStore] Renamed legacy column {oldName} to {newName}.");
        }
    }

    private bool IsOldSchema()
    {
        // Detect old schema by looking for the buff_iron_hide column
        using var pragma = _db!.CreateCommand();
        pragma.CommandText = "PRAGMA table_info(prestige)";
        using var r = pragma.ExecuteReader();
        while (r.Read())
        {
            if (r.GetString(1) == "buff_iron_hide")
                return true;
        }
        return false;
    }

    // Every step runs in one transaction. SQLite makes DDL transactional, so a crash or a throw
    // anywhere here rolls back to the original table rather than leaving the database with the old
    // table dropped and the new one not yet renamed — a state the detector above reads as
    // "already migrated", silently abandoning every player's progression.
    private void TryMigrateOldSchema()
    {
        if (!IsOldSchema())
            return;

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

        using var tx = _db!.BeginTransaction();
        _activeTx = tx;
        try
        {
            MigrateRowsInto(rows);
            tx.Commit();
        }
        catch (Exception e)
        {
            // The transaction rolls back on dispose. Rethrow: an un-migrated old-schema database
            // fails every later query anyway, so starting the round would only hide the problem.
            Log.Error($"[FSWallet] Schema migration failed and was rolled back — database untouched: {e}");
            throw;
        }
        finally
        {
            _activeTx = null;
        }

        Log.Info($"[FSWallet] Migration complete — {rows.Count} row(s) moved to new schema.");
    }

    private void MigrateRowsInto(List<(string UserId, string Username, int Ap, int Level, int Xp, int Prestige,
        string LevelsJson, string SlotsJson, string LoadoutsJson, int StoppingPower, int BulletStorm)> rows)
    {
        using (var createNew = DbCommand())
        {
            createNew.CommandText = """
                CREATE TABLE IF NOT EXISTS prestige_new (
                    user_id        TEXT PRIMARY KEY,
                    username       TEXT NOT NULL DEFAULT '',
                    perk_points INTEGER NOT NULL DEFAULT 0,
                    level          INTEGER NOT NULL DEFAULT 1,
                    experience     INTEGER NOT NULL DEFAULT 0,
                    prestige_level INTEGER NOT NULL DEFAULT 0,
                    perk_loadout   TEXT NOT NULL DEFAULT '{}',
                    prestige_buffs TEXT NOT NULL DEFAULT '{}'
                )
                """;
            createNew.ExecuteNonQuery();
        }

        foreach (var row in rows)
        {
            var augData = BuildLoadoutJson(row.LevelsJson, row.SlotsJson, row.LoadoutsJson);

            var buffDict = new Dictionary<string, int>();
            if (row.StoppingPower > 0) buffDict["StoppingPower"] = row.StoppingPower;
            if (row.BulletStorm   > 0) buffDict["BulletStorm"]   = row.BulletStorm;
            var buffJson = JsonSerializer.Serialize(buffDict);

            using var ins = DbCommand();
            ins.CommandText = """
                    INSERT INTO prestige_new (user_id, username, perk_points, level, experience,
                                             prestige_level, perk_loadout, prestige_buffs)
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

        using (var drop = DbCommand())
        {
            drop.CommandText = "DROP TABLE prestige";
            drop.ExecuteNonQuery();
        }

        using (var rename = DbCommand())
        {
            rename.CommandText = "ALTER TABLE prestige_new RENAME TO prestige";
            rename.ExecuteNonQuery();
        }
    }

    private static string BuildLoadoutJson(string levelsJson, string slotsJson, string loadoutsJson)
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

    public FullRecord GetFullRecord(Guid userId)
        => TryRead($"record load for {userId}", () => GetFullRecordCore(userId), new FullRecord(0, 1, 0, 0, "{}"));

    private FullRecord GetFullRecordCore(Guid userId)
    {
        using var cmd = DbCommand();
        cmd.CommandText = """
            SELECT perk_points, level, experience, prestige_level, prestige_buffs
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

    public int GetPerkPoints(Guid userId)
        => TryRead($"perk-point load for {userId}", () => GetPerkPointsCore(userId), 0);

    private int GetPerkPointsCore(Guid userId)
    {
        using var cmd = DbCommand();
        cmd.CommandText = "SELECT perk_points FROM prestige WHERE user_id = $id";
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        var result = cmd.ExecuteScalar();
        return result is long l ? (int)l : 0;
    }

    public void UpsertPerkPoints(Guid userId, string username, int PerkPoints)
    {
        if (!DbReady("perk-point save")) return;

        using var cmd = DbCommand();
        cmd.CommandText = """
            INSERT INTO prestige (user_id, username, perk_points)
            VALUES ($id, $name, $ap)
            ON CONFLICT(user_id) DO UPDATE SET
                username       = excluded.username,
                perk_points = excluded.perk_points
            """;
        cmd.Parameters.AddWithValue("$id",   userId.ToString());
        cmd.Parameters.AddWithValue("$name", username);
        cmd.Parameters.AddWithValue("$ap",   PerkPoints);
        TryExecute(cmd, $"perk-point save for {username}");
    }

    public void UpsertFull(Guid userId, string username,
        int PerkPoints, int level, int experience, int prestigeLevel,
        string prestigeBuffsJson = "{}")
    {
        if (!DbReady("full save")) return;

        using var cmd = DbCommand();
        cmd.CommandText = """
            INSERT INTO prestige (
                user_id, username, perk_points,
                level, experience, prestige_level, prestige_buffs)
            VALUES ($id, $name, $ap, $lvl, $xp, $prestige, $buffs)
            ON CONFLICT(user_id) DO UPDATE SET
                username       = excluded.username,
                perk_points = excluded.perk_points,
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
        TryExecute(cmd, $"full save for {username}");
    }

    public (string LevelsJson, string SlotsJson, string LoadoutsJson) LoadPerkLoadout(Guid userId)
        => TryRead($"perk-loadout load for {userId}", () => LoadPerkLoadoutCore(userId), ("", "", ""));

    private (string LevelsJson, string SlotsJson, string LoadoutsJson) LoadPerkLoadoutCore(Guid userId)
    {
        using var cmd = DbCommand();
        cmd.CommandText = "SELECT perk_loadout FROM prestige WHERE user_id = $id";
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

    public void SaveLoadoutJson(Guid userGuid, string levelsJson, string slotsJson, string loadoutsJson)
    {
        if (!DbReady("perk-loadout save")) return;

        var mergedJson = BuildLoadoutJson(levelsJson, slotsJson, loadoutsJson);

        using var cmd = DbCommand();
        cmd.CommandText = """
            INSERT INTO prestige (user_id, perk_loadout)
            VALUES ($id, $data)
            ON CONFLICT(user_id) DO UPDATE SET
                perk_loadout = excluded.perk_loadout
            """;
        cmd.Parameters.AddWithValue("$id",   userGuid.ToString());
        cmd.Parameters.AddWithValue("$data", mergedJson);
        TryExecute(cmd, $"perk-loadout save for {userGuid}");
    }
}
