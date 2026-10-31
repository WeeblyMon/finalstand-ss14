namespace Content.Server._FinalStand.Economy;

public sealed partial class FSPlayerDataStore
{
    public bool HasSeenTutorial(Guid userId, string tutorial)
        => TryRead(userId, $"tutorial check for {userId}", () =>
        {
            using var cmd = DbCommand();
            cmd.CommandText = "SELECT 1 FROM tutorials_seen WHERE user_id = $id AND tutorial = $t";
            cmd.Parameters.AddWithValue("$id", userId.ToString());
            cmd.Parameters.AddWithValue("$t", tutorial);
            return cmd.ExecuteScalar() != null;
        }, false);

    public void MarkTutorialSeen(Guid userId, string tutorial)
    {
        if (!DbReady("tutorial save"))
            return;

        using var cmd = DbCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO tutorials_seen (user_id, tutorial) VALUES ($id, $t)";
        cmd.Parameters.AddWithValue("$id", userId.ToString());
        cmd.Parameters.AddWithValue("$t", tutorial);
        TryExecute(cmd, $"tutorial save for {userId}");
    }
}
