using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Perks;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// A read that fails (a locked database, an I/O error) isn't an empty record. The wallet used to take it as one and
// save it back once the database recovered, wiping the player's real perk points.
[TestFixture]
[TestOf(typeof(FSPlayerDataStore))]
public sealed class FailedReadSaveTest : GameTest
{
    private const string ManOnFire = "ManOnFire";

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
            var db = Database(store);

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
                RetryReadsNow(store);
                Pair.ServerLogHandler.FailureLevel = failureLevel;
            }

            wallets.AddPerkPoints(mind, 5);
            Assert.That(store.GetPerkPoints(userId.UserId), Is.EqualTo(510),
                "a perk-point total read while the database failed was saved over the real one");

            store.UpsertPerkPoints(userId.UserId, "test", original);
        });
    }

    // A level or perk build whose read failed stays unloaded, rather than taking the fallback as the player's and
    // saving it over their row later, and loads once the database is back: the level on the next XP payout, the
    // build on the next state request, which also hands the body the perks slotted in it.
    [Test]
    public async Task FailedLevelAndPerkReadsLoadLater()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var store = entMan.System<FSPlayerDataStore>();
        var minds = entMan.System<SharedMindSystem>();
        var userId = Pair.Player!.UserId;
        var db = Database(store);
        var map = await Pair.CreateTestMap();

        EntityUid body = default;
        FSPlayerLevelComponent level = null;
        FSPerkLevelsComponent perks = null;
        var originalRow = default(FSPlayerDataStore.FullRecord);
        var originalLoadout = (string.Empty, string.Empty, string.Empty);
        await server.WaitAssertion(() =>
        {
            body = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var mind = minds.CreateMind(userId, "test");
            minds.TransferTo(mind, body);
            var mindId = mind.Owner;

            originalRow = store.GetFullRecord(userId.UserId);
            originalLoadout = store.LoadPerkLoadout(userId.UserId);

            var slots = new string[FSPerkDef.SlotCount];
            Array.Fill(slots, string.Empty);
            slots[0] = ManOnFire;
            store.UpsertLeveling(userId.UserId, 7, 0, 0, "{}");
            store.SaveLoadoutJson(userId.UserId,
                JsonSerializer.Serialize(new Dictionary<string, int> { [ManOnFire] = 1 }),
                JsonSerializer.Serialize(slots),
                string.Empty);

            // As left by a spawn whose reads failed.
            level = entMan.EnsureComponent<FSPlayerLevelComponent>(mindId);
            level.Loaded = false;
            perks = entMan.EnsureComponent<FSPerkLevelsComponent>(mindId);
            perks.Loaded = false;
            perks.Levels.Clear();
            Array.Fill(perks.Slots, string.Empty);
            entMan.RemoveComponent<FSManOnFireComponent>(body);
        });

        var failureLevel = Pair.ServerLogHandler.FailureLevel;
        Pair.ServerLogHandler.FailureLevel = null;
        await server.WaitPost(() => Execute(db, "ALTER TABLE prestige RENAME TO prestige_hidden"));
        try
        {
            await server.WaitPost(() =>
            {
                var ended = new WaveEndedEvent(1);
                entMan.EventBus.RaiseEvent(EventSource.Local, ref ended);
            });
            await RequestPerkState();

            await server.WaitAssertion(() =>
            {
                Assert.That(level.Loaded, Is.False, "a failed level read was taken as the player's level");
                Assert.That(perks.Loaded, Is.False, "a failed perk read was taken as the player's build");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                Execute(db, "ALTER TABLE prestige_hidden RENAME TO prestige");
                RetryReadsNow(store);
            });
            Pair.ServerLogHandler.FailureLevel = failureLevel;
        }

        await server.WaitPost(() =>
        {
            var ended = new WaveEndedEvent(1);
            entMan.EventBus.RaiseEvent(EventSource.Local, ref ended);
        });
        await RequestPerkState();

        await server.WaitAssertion(() =>
        {
            Assert.That(level.Loaded, "the level never loaded once the database was back");
            Assert.That(level.Level, Is.EqualTo(7), "the level that loaded isn't the saved one");
            Assert.That(perks.Loaded, "the perk build never loaded once the database was back");
            Assert.That(entMan.HasComponent<FSManOnFireComponent>(body), "the late-loaded build never reached the body");

            // The pair is reused; later tests shouldn't start from this one's row.
            store.UpsertLeveling(userId.UserId, originalRow.Level, originalRow.Experience, originalRow.PrestigeLevel,
                originalRow.PrestigeBuffsJson);
            store.SaveLoadoutJson(userId.UserId, originalLoadout.Item1, originalLoadout.Item2, originalLoadout.Item3);
        });
    }

    private async Task RequestPerkState()
    {
        // Past the per-player cooldown on state requests.
        await Pair.RunSeconds(1.2f);
        await Pair.Client.WaitPost(() => Pair.Client.EntMan.RaisePredictiveEvent(new FSPerkStateRequestMessage()));
        await Pair.RunTicksSync(5);
    }

    private static DbConnection Database(FSPlayerDataStore store)
    {
        return (DbConnection) typeof(FSPlayerDataStore)
            .GetField("_db", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(store)!;
    }

    // The store leaves the database alone for a few seconds after a failed read; the test's outage is over now.
    private static void RetryReadsNow(FSPlayerDataStore store)
    {
        typeof(FSPlayerDataStore)
            .GetField("_readRetryAt", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(store, TimeSpan.Zero);
    }

    private static void Execute(DbConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
