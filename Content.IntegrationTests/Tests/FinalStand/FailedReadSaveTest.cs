using System;
using System.Collections;
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
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Content.Shared.Movement.Components;
using Content.Shared.Temperature.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// A read that fails (a locked database, an I/O error) isn't an empty record. The wallet used to take it as one and
// save it back once the database recovered, wiping the player's real perk points.
[TestFixture]
[TestOf(typeof(FSPlayerDataStore))]
public sealed class FailedReadSaveTest : GameTest
{
    private const string ManOnFire = "ManOnFire";
    private const string Lightweight = "Lightweight";

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

    // After a failed read the store leaves that player's data alone for a few seconds instead of paying a query and an
    // error line on every payout. Everyone else's reads carry on.
    [Test]
    public async Task FailedReadBacksOffForThatPlayerOnly()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var store = entMan.System<FSPlayerDataStore>();
        var failing = Pair.Player!.UserId.UserId;
        var other = Guid.NewGuid();

        await server.WaitAssertion(() =>
        {
            var db = Database(store);
            var failureLevel = Pair.ServerLogHandler.FailureLevel;
            Pair.ServerLogHandler.FailureLevel = null;
            Execute(db, "ALTER TABLE prestige RENAME TO prestige_hidden");
            try
            {
                Assert.That(store.TryGetPerkPoints(failing, out _), Is.False, "the read didn't fail with the table gone");
            }
            finally
            {
                Execute(db, "ALTER TABLE prestige_hidden RENAME TO prestige");
                Pair.ServerLogHandler.FailureLevel = failureLevel;
            }

            try
            {
                Assert.That(store.TryGetPerkPoints(failing, out _), Is.False,
                    "a read straight after a failed one went back to the database");
                Assert.That(store.TryGetPerkPoints(other, out _), Is.True, "one player's failed read held up another's");
            }
            finally
            {
                RetryReadsNow(store);
            }

            Assert.That(store.TryGetPerkPoints(failing, out _), Is.True, "the read never came back after the wait");
        });
    }

    // A level or perk build whose read failed stays unloaded, rather than taking the fallback as the player's and
    // saving it over their row later, and loads once the database is back: the level on the next XP payout, the
    // build on the server's own retry, which also hands the body the perks slotted in it.
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
        Entity<MindComponent> mind = default;
        FSPlayerLevelComponent level = null;
        FSPerkLevelsComponent perks = null;
        var originalRow = default(FSPlayerDataStore.FullRecord);
        var originalLoadout = (string.Empty, string.Empty, string.Empty);
        await server.WaitAssertion(() =>
        {
            body = entMan.SpawnEntity("MobHuman", map.GridCoords);
            // The test map has no air. The damage and the cold would refresh the body's speed on their own, so only
            // the late load is left to pick up the build's speed perk.
            entMan.System<SharedGodmodeSystem>().EnableGodmode(body);
            entMan.RemoveComponent<TemperatureSpeedComponent>(body);
            mind = minds.CreateMind(userId, "test");

            originalRow = store.GetFullRecord(userId.UserId);
            originalLoadout = store.LoadPerkLoadout(userId.UserId);

            var slots = new string[FSPerkDef.SlotCount];
            Array.Fill(slots, string.Empty);
            slots[0] = ManOnFire;
            slots[1] = Lightweight;
            store.UpsertLeveling(userId.UserId, 7, 0, 0, "{}");
            store.SaveLoadoutJson(userId.UserId,
                JsonSerializer.Serialize(new Dictionary<string, int> { [ManOnFire] = 1, [Lightweight] = 1 }),
                JsonSerializer.Serialize(slots),
                string.Empty);

            // As left by a spawn whose level read failed.
            level = entMan.EnsureComponent<FSPlayerLevelComponent>(mind);
            level.Loaded = false;
        });

        var baseSpeed = 0f;
        var failureLevel = Pair.ServerLogHandler.FailureLevel;
        Pair.ServerLogHandler.FailureLevel = null;
        await server.WaitPost(() => Execute(db, "ALTER TABLE prestige RENAME TO prestige_hidden"));
        try
        {
            await server.WaitPost(() =>
            {
                // Taking over the body loads the build, which fails with the table gone.
                minds.TransferTo(mind, body);
                var ended = new WaveEndedEvent(1);
                entMan.EventBus.RaiseEvent(EventSource.Local, ref ended);
            });
            await Pair.RunSeconds(1);

            await server.WaitAssertion(() =>
            {
                perks = entMan.GetComponent<FSPerkLevelsComponent>(mind);
                Assert.That(level.Loaded, Is.False, "a failed level read was taken as the player's level");
                Assert.That(perks.Loaded, Is.False, "a failed perk read was taken as the player's build");
                Assert.That(entMan.HasComponent<FSManOnFireComponent>(body), Is.False, "the body got a build that never loaded");
                baseSpeed = entMan.GetComponent<MovementSpeedModifierComponent>(body).CurrentSprintSpeed;
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

        // Nothing asks for the build again; the server retries it every few seconds.
        await Pair.RunSeconds(6);

        await server.WaitAssertion(() =>
        {
            try
            {
                Assert.That(level.Loaded, "the level never loaded once the database was back");
                Assert.That(level.Level, Is.EqualTo(7), "the level that loaded isn't the saved one");
                Assert.That(perks.Loaded, "the perk build never loaded once the database was back");
                Assert.That(entMan.HasComponent<FSManOnFireComponent>(body), "the late-loaded build never reached the body");
                Assert.That(entMan.GetComponent<MovementSpeedModifierComponent>(body).CurrentSprintSpeed,
                    Is.GreaterThan(baseSpeed), "the body kept its speed from before the build loaded");
            }
            finally
            {
                // The pair is reused; later tests shouldn't start from this one's row. Unloaded copies are never saved,
                // so the payout's XP and the build don't get written back over the restore.
                level.Loaded = false;
                perks.Loaded = false;
                store.UpsertLeveling(userId.UserId, originalRow.Level, originalRow.Experience, originalRow.PrestigeLevel,
                    originalRow.PrestigeBuffsJson);
                store.SaveLoadoutJson(userId.UserId, originalLoadout.Item1, originalLoadout.Item2, originalLoadout.Item3);
            }
        });
    }

    private static DbConnection Database(FSPlayerDataStore store)
    {
        return (DbConnection) typeof(FSPlayerDataStore)
            .GetField("_db", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(store)!;
    }

    // The store leaves a player's data alone for a few seconds after a failed read; the test's outage is over now.
    private static void RetryReadsNow(FSPlayerDataStore store)
    {
        ((IDictionary) typeof(FSPlayerDataStore)
            .GetField("_readRetryAt", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(store)!).Clear();
    }

    private static void Execute(DbConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
