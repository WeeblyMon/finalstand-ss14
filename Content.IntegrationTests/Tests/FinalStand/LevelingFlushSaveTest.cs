using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server.GameTicking;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

// XP is saved in batches. A server stop deletes the minds before systems shut down, so the last batch has to go out
// before the flush, or XP earned by a player who isn't in their body (disconnected, ghosted) is lost.
[TestFixture]
[TestOf(typeof(FSLevelingSystem))]
public sealed class LevelingFlushSaveTest : GameTest
{
    // Destructive: the test flushes every entity.
    public override PoolSettings PoolSettings => new()
    {
        Destructive = true,
        Connected = true,
        DummyTicker = false,
        Map = PoolManager.TestStation,
    };

    private static readonly EntProtoId WaveRule = "WaveSurvivalZombie";

    [Test]
    public async Task UnsavedXpIsWrittenBeforeAFlush()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var players = server.ResolveDependency<IPlayerManager>();
        var ticker = entMan.System<GameTicker>();
        var minds = entMan.System<SharedMindSystem>();
        var store = entMan.System<FSPlayerDataStore>();

        await server.WaitPost(() => Assert.That(ticker.StartGameRule(WaveRule, out _)));
        await Pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var session = Pair.Player!;
            Assert.That(minds.TryGetMind(session, out var mindId, out _), "the test player has no mind");
            Assert.That(entMan.TryGetComponent(mindId, out FSPlayerLevelComponent lvl) && lvl.Loaded, "leveling never loaded");

            // Out of the body, so detaching (which saves) is already behind us.
            players.SetAttachedEntity(session, null);

            var before = lvl.Experience;
            var ev = new WaveEndedEvent(1);
            entMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
            Assert.That(lvl.Experience, Is.GreaterThan(before), "the wave clear gave no XP");

            var earned = lvl.Experience;
            var level = lvl.Level;
            entMan.FlushEntities();

            var record = store.GetFullRecord(session.UserId.UserId);
            Assert.Multiple(() =>
            {
                Assert.That(record.Level, Is.EqualTo(level));
                Assert.That(record.Experience, Is.EqualTo(earned), "the XP earned since the last save was lost");
            });
        });
    }
}
