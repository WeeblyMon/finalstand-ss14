using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.GameTicking;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Tutorial;
using Content.Shared._FinalStand.Tutorial;
using Content.Shared.GameTicking;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class TutorialTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task EachTipShowsOncePerPlayerAndSurvivesRestarts()
    {
        await Server.WaitAssertion(() =>
        {
            var tutorial = SEntMan.System<FSTutorialSystem>();
            var store = SEntMan.System<FSPlayerDataStore>();
            var session = ServerSession!;
            store.DeleteProgress(session.UserId.UserId);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            Assert.That(session.Status, Is.EqualTo(SessionStatus.InGame), "test precondition");
            Assert.That(tutorial.WantsTips(session), Is.True, "a fresh rookie gets tips");

            Assert.That(tutorial.TryShow(session, FSTutorialTip.Welcome), Is.True);
            Assert.That(tutorial.TryShow(session, FSTutorialTip.Welcome), Is.False, "each tip shows once");
            Assert.That(tutorial.TryShow(session, FSTutorialTip.Downed), Is.True, "other tips are separate");

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Assert.Multiple(() =>
            {
                Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.Welcome), Is.True, "seen tips are saved");
                Assert.That(tutorial.TryShow(session, FSTutorialTip.Welcome), Is.False);
                Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.BossWave), Is.False);
            });
        });
    }

    [Test]
    public async Task PlayersCanTurnTipsOffAndReplayThem()
    {
        await Server.WaitAssertion(() =>
        {
            var tutorial = SEntMan.System<FSTutorialSystem>();
            var session = ServerSession!;
            SEntMan.System<FSPlayerDataStore>().DeleteProgress(session.UserId.UserId);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            tutorial.SetEnabled(session.UserId, false);
            Assert.That(tutorial.WantsTips(session), Is.False);
            Assert.That(tutorial.TryShow(session, FSTutorialTip.Prep), Is.False, "opted out");

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Assert.That(tutorial.WantsTips(session), Is.False, "opting out is saved");

            tutorial.TryShow(session, FSTutorialTip.Welcome);
            tutorial.Replay(session.UserId);
            Assert.Multiple(() =>
            {
                Assert.That(tutorial.WantsTips(session), Is.True, "replay turns tips back on");
                Assert.That(tutorial.TryShow(session, FSTutorialTip.Prep), Is.True);
            });
        });
    }
}

[TestFixture]
public sealed class EnemyTipTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    [Test]
    public async Task EverySpecialZombieHasATipWithText()
    {
        await Server.WaitAssertion(() =>
        {
            var loc = Server.ResolveDependency<Robust.Shared.Localization.ILocalizationManager>();
            var rule = SProtoMan.Index(FSTestRules.WaveSurvival);
            Assert.That(rule.TryComp<WaveGameRuleComponent>(out var waves, SEntMan.ComponentFactory), Is.True);
            var specials = waves!.SpecialEnemyPool.Select(s => s.EnemyId.Id)
                .Concat(waves.EnemyConfigs.SelectMany(c => c.EnemyPool).Select(p => p.Id))
                .Where(id => id != "FSZombieNormal")
                .Distinct()
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(specials, Is.Not.Empty, "test precondition");
                foreach (var id in specials)
                    Assert.That(FSTutorialText.ForEnemy(id), Is.Not.Null, $"{id} spawns in waves but has no tip");

                foreach (var tip in Enum.GetValues<FSTutorialTip>())
                {
                    Assert.That(loc.HasString(FSTutorialText.TitleLocId(tip)), Is.True, $"{tip} has no title");
                    Assert.That(loc.HasString(FSTutorialText.BodyLocId(tip)), Is.True, $"{tip} has no body");
                }

                var keys = Enum.GetValues<FSTutorialTip>().Select(FSTutorialText.Key).ToList();
                Assert.That(keys, Is.Unique, "every tip saves under its own key");
            });
        });
    }

    [Test]
    public async Task TheFirstBloaterOfTheRoundExplainsItself()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var tutorial = SEntMan.System<FSTutorialSystem>();
            var spawning = SEntMan.System<WaveEnemySpawningSystem>();
            var session = ServerSession!;
            SEntMan.System<FSPlayerDataStore>().DeleteProgress(session.UserId.UserId);

            Assert.That(session.AttachedEntity, Is.Not.Null, "test precondition");
            Assert.That(tutorial.WantsTips(session), Is.True, "test precondition");
            Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival, out var ruleUid), Is.True);
            var rule = SEntMan.GetComponent<WaveGameRuleComponent>(ruleUid);
            Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.Bloater), Is.False, "test precondition");

            spawning.SpawnWaveEnemy("FSZombieBloater", map.GridCoords, rule);
            Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.Bloater), Is.True);
            Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.Tesla), Is.False, "only the zombie that showed up is explained");

            spawning.SpawnWaveEnemy("FSZombieNormal", map.GridCoords, rule);
            Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.Runner), Is.False, "plain zombies need no tip");
        });
    }
}

[TestFixture]
public sealed class TutorialCardTest : GameTest
{
    [Test]
    public async Task TipsQueueAndDismissInOrder()
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<Robust.Client.UserInterface.IUserInterfaceManager>()
                .GetUIController<Content.Client._FinalStand.Tutorial.FSTutorialUIController>();
            ui.Reset();

            ui.Enqueue(FSTutorialTip.Welcome);
            ui.Enqueue(FSTutorialTip.Prep);
            ui.Enqueue(FSTutorialTip.Prep);
            Assert.That(ui.Showing, Is.EqualTo(FSTutorialTip.Welcome));

            ui.Dismiss();
            Assert.That(ui.Showing, Is.EqualTo(FSTutorialTip.Prep), "queued tips follow in order");

            ui.Dismiss();
            Assert.That(ui.Showing, Is.Null, "a duplicate tip is not queued twice");
        });
    }
}
