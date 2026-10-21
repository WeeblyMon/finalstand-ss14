using System.Data.Common;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// A read that fails (a locked database, an I/O error) isn't an empty record. The wallet used to take it as one and
// save it back once the database recovered, wiping the player's real perk points.
[TestFixture]
[TestOf(typeof(FSPlayerDataStore))]
public sealed class FailedReadSaveTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task FailedReadIsNeverSavedOver()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var store = entMan.System<FSPlayerDataStore>();
        var wallets = entMan.System<FSPlayerWalletSystem>();
        var minds = entMan.System<SharedMindSystem>();
        var userId = Pair.Player!.UserId;

        await server.WaitAssertion(() =>
        {
            var db = (DbConnection) typeof(FSPlayerDataStore)
                .GetField("_db", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(store)!;

            var original = store.GetPerkPoints(userId.UserId);
            store.UpsertPerkPoints(userId.UserId, "test", 500);
            var mind = minds.CreateMind(userId, "test");

            // Reads fail while the table is out of the way. They log an error, which is the point here.
            var failureLevel = Pair.ServerLogHandler.FailureLevel;
            Pair.ServerLogHandler.FailureLevel = null;
            Execute(db, "ALTER TABLE prestige RENAME TO prestige_hidden");
            try
            {
                wallets.AddPerkPoints(mind, 5);
            }
            finally
            {
                Execute(db, "ALTER TABLE prestige_hidden RENAME TO prestige");
                Pair.ServerLogHandler.FailureLevel = failureLevel;
            }

            wallets.AddPerkPoints(mind, 5);
            Assert.That(store.GetPerkPoints(userId.UserId), Is.EqualTo(510),
                "a perk-point total read while the database failed was saved over the real one");

            store.UpsertPerkPoints(userId.UserId, "test", original);
        });
    }

    private static void Execute(DbConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
