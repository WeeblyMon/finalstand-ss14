using Content.IntegrationTests.Fixtures;
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
public sealed class TutorialCardTest : GameTest
{
    [Test]
    public async Task TipsQueueAndDismissInOrder()
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<Robust.Client.UserInterface.IUserInterfaceManager>()
                .GetUIController<Content.Client._FinalStand.Tutorial.FSTutorialUIController>();

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
