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
            CREATE TABLE IF NOT EXISTS fs_titles (
                user_id   TEXT NOT NULL,
                title_id  TEXT NOT NULL,
                earned_at TEXT NOT NULL,
                PRIMARY KEY (user_id, title_id)
            );
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
        => TryRead($"onboarding load for {userId}", () => GetOnboardingCore(userId), default);

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
        if (!DbReady("onboarding save")) return;

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
        => TryRead($"title load for {userId}", () => GetTitlesCore(userId), new List<string>());

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
        => TryRead($"profile load for {userId}", () => GetProfileCore(userId), default);

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
        if (!DbReady("profile save")) return;

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

        foreach (var table in new[] { "fs_onboarding", "fs_titles", "fs_profile" })
        {
            using var cmd = DbCommand();
            cmd.CommandText = $"DELETE FROM {table} WHERE user_id = $id";
            cmd.Parameters.AddWithValue("$id", userId.ToString());
            TryExecute(cmd, $"{table} delete for {userId}");
        }
    }
}
