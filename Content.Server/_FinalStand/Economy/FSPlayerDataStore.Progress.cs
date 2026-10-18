using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using Microsoft.Data.Sqlite;

namespace Content.Server._FinalStand.Economy;

public sealed partial class FSPlayerDataStore
{
    public record struct OnboardingRecord(bool Known, bool WasRookie, bool StarterGranted, bool FirstRoundPaid, bool Graduated);

    public record struct ProfileRecord(
        string? EquippedTitle,
        int FurthestWave,
        int BestRoundKills,
        int TotalKills,
        int TotalRevives,
        int RoundsPlayed,
        int Victories);

    private static void InitProgressSchema(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS fs_onboarding (
                user_id          TEXT PRIMARY KEY,
                was_rookie       INTEGER NOT NULL DEFAULT 0,
                starter_granted  INTEGER NOT NULL DEFAULT 0,
                first_round_paid INTEGER NOT NULL DEFAULT 0,
                graduated        INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS fs_tutorial (
                user_id TEXT NOT NULL,
                tip     TEXT NOT NULL,
                PRIMARY KEY (user_id, tip)
            );
            CREATE TABLE IF NOT EXISTS fs_titles (
                user_id   TEXT NOT NULL,
                title_id  TEXT NOT NULL,
                earned_at TEXT NOT NULL,
                PRIMARY KEY (user_id, title_id)
            );
            CREATE TABLE IF NOT EXISTS fs_runs (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                ended_at         TEXT NOT NULL,
                difficulty       INTEGER NOT NULL,
                bracket          INTEGER NOT NULL,
                waves            INTEGER NOT NULL,
                victory          INTEGER NOT NULL,
                duration_seconds INTEGER NOT NULL,
                kills            INTEGER NOT NULL,
                players          TEXT NOT NULL,
                weekly           TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS fs_runs_weekly ON fs_runs (weekly, victory DESC, waves DESC, duration_seconds ASC);
            CREATE TABLE IF NOT EXISTS fs_weekly_rewards (
                user_id  TEXT NOT NULL,
                week_key TEXT NOT NULL,
                PRIMARY KEY (user_id, week_key)
            );
            CREATE TABLE IF NOT EXISTS fs_mastery (
                user_id TEXT NOT NULL,
                perk_id TEXT NOT NULL,
                kills   INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (user_id, perk_id)
            );
            CREATE TABLE IF NOT EXISTS fs_mentor (
                user_id     TEXT PRIMARY KEY,
                total       INTEGER NOT NULL DEFAULT 0,
                balance     INTEGER NOT NULL DEFAULT 0,
                week_key    TEXT NOT NULL DEFAULT '',
                week_points INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS fs_meta (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS fs_runs_ladder ON fs_runs (difficulty, bracket, victory DESC, waves DESC, duration_seconds ASC);
            CREATE TABLE IF NOT EXISTS fs_profile (
                user_id          TEXT PRIMARY KEY,
                equipped_title   TEXT NULL,
                furthest_wave    INTEGER NOT NULL DEFAULT 0,
                best_round_kills INTEGER NOT NULL DEFAULT 0,
                total_kills      INTEGER NOT NULL DEFAULT 0,
                total_revives    INTEGER NOT NULL DEFAULT 0,
                rounds_played    INTEGER NOT NULL DEFAULT 0,
                victories        INTEGER NOT NULL DEFAULT 0
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public OnboardingRecord GetOnboarding(Guid userId)
        => ReadProgress(userId, $"onboarding load for {userId}", () => GetOnboardingCore(userId), default);

    private OnboardingRecord GetOnboardingCore(Guid userId)
    {
        using var cmd = DbCommand();
        cmd.CommandText = """
            SELECT was_rookie, starter_granted, first_round_paid, graduated
            FROM fs_onboarding WHERE user_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return default;

        return new OnboardingRecord(true, r.GetInt32(0) != 0, r.GetInt32(1) != 0, r.GetInt32(2) != 0, r.GetInt32(3) != 0);
    }

    public void SaveOnboarding(Guid userId, OnboardingRecord record)
    {
        if (!ProgressWritable(userId, "onboarding save")) return;

        using var cmd = DbCommand();
        cmd.CommandText = """
            INSERT INTO fs_onboarding (user_id, was_rookie, starter_granted, first_round_paid, graduated)
            VALUES ($id, $rookie, $starter, $first, $grad)
            ON CONFLICT(user_id) DO UPDATE SET
                was_rookie       = excluded.was_rookie,
                starter_granted  = excluded.starter_granted,
                first_round_paid = excluded.first_round_paid,
                graduated        = excluded.graduated
            """;
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        cmd.Parameters.AddWithValue("$rookie", record.WasRookie ? 1 : 0);
        cmd.Parameters.AddWithValue("$starter", record.StarterGranted ? 1 : 0);
        cmd.Parameters.AddWithValue("$first", record.FirstRoundPaid ? 1 : 0);
        cmd.Parameters.AddWithValue("$grad", record.Graduated ? 1 : 0);
        TryExecute(cmd, $"onboarding save for {userId}");
    }

    public List<string> GetTitles(Guid userId)
        => ReadProgress(userId, $"title load for {userId}", () => GetTitlesCore(userId), new List<string>());

    private List<string> GetTitlesCore(Guid userId)
    {
        using var cmd = DbCommand();
        cmd.CommandText = "SELECT title_id FROM fs_titles WHERE user_id = $id ORDER BY earned_at, title_id";
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        using var r = cmd.ExecuteReader();
        var titles = new List<string>();
        while (r.Read())
            titles.Add(r.GetString(0));
        return titles;
    }

    public bool AddTitle(Guid userId, string titleId, DateTime earnedAtUtc)
    {
        if (!DbReady("title grant")) return false;

        using var cmd = DbCommand();
        cmd.CommandText = """
            INSERT INTO fs_titles (user_id, title_id, earned_at)
            VALUES ($id, $title, $at)
            ON CONFLICT(user_id, title_id) DO NOTHING
            """;
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        cmd.Parameters.AddWithValue("$title", titleId);
        cmd.Parameters.AddWithValue("$at", earnedAtUtc.ToString("O"));
        return TryExecuteCounted(cmd, $"title grant {titleId} for {userId}") > 0;
    }

    public int CountTitleHolders(string titleId)
        => TryRead($"title holder count for {titleId}", () =>
        {
            using var cmd = DbCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM fs_titles WHERE title_id = $title";
            cmd.Parameters.AddWithValue("$title", titleId);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }, 0);

    public ProfileRecord GetProfile(Guid userId)
        => ReadProgress(userId, $"profile load for {userId}", () => GetProfileCore(userId), default);

    private ProfileRecord GetProfileCore(Guid userId)
    {
        using var cmd = DbCommand();
        cmd.CommandText = """
            SELECT equipped_title, furthest_wave, best_round_kills, total_kills, total_revives, rounds_played, victories
            FROM fs_profile WHERE user_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return default;

        return new ProfileRecord(
            r.IsDBNull(0) ? null : r.GetString(0),
            r.GetInt32(1),
            r.GetInt32(2),
            r.GetInt32(3),
            r.GetInt32(4),
            r.GetInt32(5),
            r.GetInt32(6));
    }

    public void SaveProfile(Guid userId, ProfileRecord profile)
    {
        if (!ProgressWritable(userId, "profile save")) return;

        using var cmd = DbCommand();
        cmd.CommandText = """
            INSERT INTO fs_profile (user_id, equipped_title, furthest_wave, best_round_kills,
                                    total_kills, total_revives, rounds_played, victories)
            VALUES ($id, $title, $wave, $best, $kills, $revives, $rounds, $wins)
            ON CONFLICT(user_id) DO UPDATE SET
                equipped_title   = excluded.equipped_title,
                furthest_wave    = excluded.furthest_wave,
                best_round_kills = excluded.best_round_kills,
                total_kills      = excluded.total_kills,
                total_revives    = excluded.total_revives,
                rounds_played    = excluded.rounds_played,
                victories        = excluded.victories
            """;
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        cmd.Parameters.AddWithValue("$title", (object?) profile.EquippedTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$wave", profile.FurthestWave);
        cmd.Parameters.AddWithValue("$best", profile.BestRoundKills);
        cmd.Parameters.AddWithValue("$kills", profile.TotalKills);
        cmd.Parameters.AddWithValue("$revives", profile.TotalRevives);
        cmd.Parameters.AddWithValue("$rounds", profile.RoundsPlayed);
        cmd.Parameters.AddWithValue("$wins", profile.Victories);
        TryExecute(cmd, $"profile save for {userId}");
    }

    public void DeleteProgress(Guid userId)
    {
        if (!DbReady("progress delete")) return;

        foreach (var table in new[] { "fs_onboarding", "fs_titles", "fs_profile", "fs_mastery", "fs_weekly_rewards", "fs_mentor", "fs_tutorial" })
        {
            using var cmd = DbCommand();
            cmd.CommandText = $"DELETE FROM {table} WHERE user_id = $id";
            cmd.Parameters.AddWithValue("$id", userId.ToString());
            TryExecute(cmd, $"{table} delete for {userId}");
        }
    }

    public long? InsertRun(FSRunRecord run)
    {
        if (!DbReady("run save")) return null;

        using var cmd = DbCommand();
        cmd.CommandText = """
            INSERT INTO fs_runs (ended_at, difficulty, bracket, waves, victory, duration_seconds, kills, players, weekly)
            VALUES ($at, $difficulty, $bracket, $waves, $victory, $duration, $kills, $players, $weekly)
            RETURNING id
            """;
        AddRunParameters(cmd, run);
        try
        {
            return cmd.ExecuteScalar() is long id ? id : null;
        }
        catch (Exception e)
        {
            Log.Error($"[FSWallet] run save failed: {e.Message}");
            return null;
        }
    }

    public void UpdateRun(long id, FSRunRecord run)
    {
        if (!DbReady("run update")) return;

        using var cmd = DbCommand();
        cmd.CommandText = """
            UPDATE fs_runs SET ended_at = $at, difficulty = $difficulty, bracket = $bracket, waves = $waves,
                victory = $victory, duration_seconds = $duration, kills = $kills, players = $players, weekly = $weekly
            WHERE id = $id
            """;
        AddRunParameters(cmd, run);
        cmd.Parameters.AddWithValue("$id", id);
        TryExecute(cmd, "run update");
    }

    public void DeleteRun(long id)
    {
        if (!DbReady("run delete")) return;

        using var cmd = DbCommand();
        cmd.CommandText = "DELETE FROM fs_runs WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        TryExecute(cmd, "run delete");
    }

    private static void AddRunParameters(SqliteCommand cmd, FSRunRecord run)
    {
        cmd.Parameters.AddWithValue("$at", run.EndedAtUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$difficulty", (int) run.Difficulty);
        cmd.Parameters.AddWithValue("$bracket", (int) run.Bracket);
        cmd.Parameters.AddWithValue("$waves", run.Waves);
        cmd.Parameters.AddWithValue("$victory", run.Victory ? 1 : 0);
        cmd.Parameters.AddWithValue("$duration", run.DurationSeconds);
        cmd.Parameters.AddWithValue("$kills", run.Kills);
        cmd.Parameters.AddWithValue("$players", run.Players);
        cmd.Parameters.AddWithValue("$weekly", (object?) run.Weekly ?? DBNull.Value);
    }

    public List<FSRunRecord> GetTopRuns(FSDifficulty difficulty, FSPlayerBracket? bracket, int limit)
        => TryRead($"ladder load for {difficulty}/{bracket}", () =>
        {
            using var cmd = DbCommand();
            cmd.CommandText = """
                SELECT players, waves, victory, duration_seconds, kills, ended_at, bracket
                FROM fs_runs
                WHERE difficulty = $difficulty AND ($bracket IS NULL OR bracket = $bracket) AND weekly IS NULL
                ORDER BY victory DESC, waves DESC, duration_seconds ASC, ended_at ASC
                LIMIT $limit
                """;
            cmd.Parameters.AddWithValue("$difficulty", (int) difficulty);
            cmd.Parameters.AddWithValue("$bracket", bracket is { } b ? (int) b : DBNull.Value);
            cmd.Parameters.AddWithValue("$limit", limit);
            using var r = cmd.ExecuteReader();
            var runs = new List<FSRunRecord>();
            while (r.Read())
            {
                runs.Add(new FSRunRecord(
                    r.GetString(0),
                    difficulty,
                    (FSPlayerBracket) r.GetInt32(6),
                    r.GetInt32(1),
                    r.GetInt32(2) != 0,
                    r.GetInt32(3),
                    r.GetInt32(4),
                    DateTime.Parse(r.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind)));
            }
            return runs;
        }, new List<FSRunRecord>());

    public void DeleteAllRuns()
    {
        if (!DbReady("run wipe")) return;

        using var cmd = DbCommand();
        cmd.CommandText = "DELETE FROM fs_runs";
        TryExecute(cmd, "run wipe");
    }

    public int CountKnownPlayers()
        => TryRead("player count", () =>
        {
            using var cmd = DbCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM (SELECT user_id FROM prestige UNION SELECT user_id FROM fs_profile)";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }, 0);

    public List<FSRunRecord> GetWeeklyTopRuns(string weekKey, int limit)
        => TryRead($"weekly ladder load for {weekKey}", () =>
        {
            using var cmd = DbCommand();
            cmd.CommandText = """
                SELECT players, waves, victory, duration_seconds, kills, ended_at, difficulty, bracket
                FROM fs_runs
                WHERE weekly = $week
                ORDER BY victory DESC, waves DESC, duration_seconds ASC, ended_at ASC
                LIMIT $limit
                """;
            cmd.Parameters.AddWithValue("$week", weekKey);
            cmd.Parameters.AddWithValue("$limit", limit);
            using var r = cmd.ExecuteReader();
            var runs = new List<FSRunRecord>();
            while (r.Read())
            {
                runs.Add(new FSRunRecord(
                    r.GetString(0),
                    (FSDifficulty) r.GetInt32(6),
                    (FSPlayerBracket) r.GetInt32(7),
                    r.GetInt32(1),
                    r.GetInt32(2) != 0,
                    r.GetInt32(3),
                    r.GetInt32(4),
                    DateTime.Parse(r.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    weekKey));
            }
            return runs;
        }, new List<FSRunRecord>());

    public bool TryClaimWeeklyReward(Guid userId, string weekKey)
    {
        if (!DbReady("weekly reward claim")) return false;

        using var cmd = DbCommand();
        cmd.CommandText = """
            INSERT INTO fs_weekly_rewards (user_id, week_key) VALUES ($id, $week)
            ON CONFLICT(user_id, week_key) DO NOTHING
            """;
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        cmd.Parameters.AddWithValue("$week", weekKey);
        return TryExecuteCounted(cmd, $"weekly reward claim for {userId}") > 0;
    }

    public string? GetMeta(string key)
        => TryRead($"meta load {key}", () =>
        {
            using var cmd = DbCommand();
            cmd.CommandText = "SELECT value FROM fs_meta WHERE key = $key";
            cmd.Parameters.AddWithValue("$key", key);
            return cmd.ExecuteScalar() as string;
        }, null);

    public void SetMeta(string key, string value)
    {
        if (!DbReady("meta save")) return;

        using var cmd = DbCommand();
        cmd.CommandText = """
            INSERT INTO fs_meta (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        TryExecute(cmd, $"meta save {key}");
    }

    public Dictionary<string, int> GetMastery(Guid userId)
        => ReadProgress(userId, $"mastery load for {userId}", () =>
        {
            using var cmd = DbCommand();
            cmd.CommandText = "SELECT perk_id, kills FROM fs_mastery WHERE user_id = $id";
            cmd.Parameters.AddWithValue("$id", userId.ToString());
            using var r = cmd.ExecuteReader();
            var result = new Dictionary<string, int>();
            while (r.Read())
                result[r.GetString(0)] = r.GetInt32(1);
            return result;
        }, new Dictionary<string, int>());

    public void SaveMastery(Guid userId, string perkId, int kills)
    {
        if (!ProgressWritable(userId, "mastery save")) return;

        using var cmd = DbCommand();
        cmd.CommandText = """
            INSERT INTO fs_mastery (user_id, perk_id, kills) VALUES ($id, $perk, $kills)
            ON CONFLICT(user_id, perk_id) DO UPDATE SET kills = excluded.kills
            """;
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        cmd.Parameters.AddWithValue("$perk", perkId);
        cmd.Parameters.AddWithValue("$kills", kills);
        TryExecute(cmd, $"mastery save {perkId} for {userId}");
    }

    public record struct MentorRecord(int Total, int Balance, string WeekKey, int WeekPoints);

    public MentorRecord GetMentor(Guid userId)
        => ReadProgress(userId, $"mentor load for {userId}", () =>
        {
            using var cmd = DbCommand();
            cmd.CommandText = "SELECT total, balance, week_key, week_points FROM fs_mentor WHERE user_id = $id";
            cmd.Parameters.AddWithValue("$id", userId.ToString());
            using var r = cmd.ExecuteReader();
            return r.Read()
                ? new MentorRecord(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetInt32(3))
                : new MentorRecord(0, 0, string.Empty, 0);
        }, new MentorRecord(0, 0, string.Empty, 0));

    public void SaveMentor(Guid userId, MentorRecord record)
    {
        if (!ProgressWritable(userId, "mentor save")) return;

        using var cmd = DbCommand();
        cmd.CommandText = """
            INSERT INTO fs_mentor (user_id, total, balance, week_key, week_points)
            VALUES ($id, $total, $balance, $week, $weekPoints)
            ON CONFLICT(user_id) DO UPDATE SET
                total = excluded.total,
                balance = excluded.balance,
                week_key = excluded.week_key,
                week_points = excluded.week_points
            """;
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        cmd.Parameters.AddWithValue("$total", record.Total);
        cmd.Parameters.AddWithValue("$balance", record.Balance);
        cmd.Parameters.AddWithValue("$week", record.WeekKey);
        cmd.Parameters.AddWithValue("$weekPoints", record.WeekPoints);
        TryExecute(cmd, $"mentor save for {userId}");
    }

    public HashSet<string> GetTutorialFlags(Guid userId)
        => ReadProgress(userId, $"tutorial load for {userId}", () =>
        {
            using var cmd = DbCommand();
            cmd.CommandText = "SELECT tip FROM fs_tutorial WHERE user_id = $id";
            cmd.Parameters.AddWithValue("$id", userId.ToString());
            using var r = cmd.ExecuteReader();
            var flags = new HashSet<string>();
            while (r.Read())
                flags.Add(r.GetString(0));
            return flags;
        }, new HashSet<string>());

    public void SetTutorialFlag(Guid userId, string flag, bool on)
    {
        if (!DbReady("tutorial save")) return;

        using var cmd = DbCommand();
        cmd.CommandText = on
            ? "INSERT INTO fs_tutorial (user_id, tip) VALUES ($id, $tip) ON CONFLICT(user_id, tip) DO NOTHING"
            : "DELETE FROM fs_tutorial WHERE user_id = $id AND tip = $tip";
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        cmd.Parameters.AddWithValue("$tip", flag);
        TryExecute(cmd, $"tutorial flag {flag} for {userId}");
    }

    public void ClearTutorial(Guid userId)
    {
        if (!DbReady("tutorial reset")) return;

        using var cmd = DbCommand();
        cmd.CommandText = "DELETE FROM fs_tutorial WHERE user_id = $id";
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        TryExecute(cmd, $"tutorial reset for {userId}");
    }

    private readonly HashSet<Guid> _unreadable = new();

    public bool IsProgressReadable(Guid userId) => !_unreadable.Contains(userId);

    private T ReadProgress<T>(Guid userId, string what, Func<T> read, T fallback)
    {
        if (!DbReady(what))
            return fallback;

        try
        {
            return read();
        }
        catch (Exception e)
        {
            _unreadable.Add(userId);
            Log.Error($"[FSWallet] {what} failed, so this player's progress won't be saved until restart: {e.Message}");
            return fallback;
        }
    }

    private bool ProgressWritable(Guid userId, string what)
    {
        if (!DbReady(what))
            return false;
        if (!_unreadable.Contains(userId))
            return true;

        Log.Warning($"[FSWallet] {what} for {userId} skipped: their saved progress couldn't be read, so saving now would overwrite it");
        return false;
    }
}
