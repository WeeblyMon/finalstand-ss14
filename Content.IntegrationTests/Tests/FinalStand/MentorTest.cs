using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Mentor;
using Content.Server._FinalStand.Titles;
using Content.Shared._FinalStand.GameTicking;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class MentorTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task MentorPointsBuyTitlesOnce()
    {
        await Server.WaitAssertion(() =>
        {
            var mentor = SEntMan.System<FSMentorSystem>();
            var store = SEntMan.System<FSPlayerDataStore>();
            var titles = SEntMan.System<FSTitleSystem>();
            var user = ServerSession!.UserId;
            store.DeleteProgress(user.UserId);

            var week = FSWeeklyRules.WeekKey(System.DateTime.UtcNow);
            store.SaveMentor(user.UserId, new FSPlayerDataStore.MentorRecord(60, 60, week, 20));

            Assert.That(mentor.TryBuy(user, "guardian", out _), Is.False, "400 points needed");
            Assert.That(mentor.TryBuy(user, "nonsense", out _), Is.False);
            Assert.That(mentor.TryBuy(user, "MENTOR", out var message), Is.True, message);
            Assert.That(titles.GetOwned(user).Select(t => t.Id), Does.Contain("FSTitleMentor"));

            var after = mentor.CurrentRecord(user.UserId);
            Assert.Multiple(() =>
            {
                Assert.That(after.Balance, Is.EqualTo(10));
                Assert.That(after.Total, Is.EqualTo(60), "spending never lowers the lifetime total");
            });

            store.SaveMentor(user.UserId, after with { Balance = 100 });
            Assert.That(mentor.TryBuy(user, "mentor", out _), Is.False, "a title can't be bought twice");
            Assert.That(mentor.CurrentRecord(user.UserId).Balance, Is.EqualTo(100));
        });
    }

    [Test]
    public async Task WeeklyPointsResetOnANewWeek()
    {
        await Server.WaitAssertion(() =>
        {
            var mentor = SEntMan.System<FSMentorSystem>();
            var store = SEntMan.System<FSPlayerDataStore>();
            var user = ServerSession!.UserId.UserId;
            store.DeleteProgress(user);

            store.SaveMentor(user, new FSPlayerDataStore.MentorRecord(200, 50, "2001-W01", FSMentorLedger.WeekCap));
            var record = mentor.CurrentRecord(user);
            Assert.Multiple(() =>
            {
                Assert.That(record.WeekPoints, Is.Zero);
                Assert.That(record.WeekKey, Is.EqualTo(FSWeeklyRules.WeekKey(System.DateTime.UtcNow)));
                Assert.That(record.Balance, Is.EqualTo(50));
                Assert.That(record.Total, Is.EqualTo(200));
            });
        });
    }

    [Test]
    public async Task ARookieCannotMentorThemselves()
    {
        await Server.WaitAssertion(() =>
        {
            var mentor = SEntMan.System<FSMentorSystem>();
            var session = ServerSession!;
            SEntMan.System<FSPlayerDataStore>().DeleteProgress(session.UserId.UserId);

            Assert.Multiple(() =>
            {
                Assert.That(mentor.IsEligibleMentor(session), Is.False, "a player with no playtime or levels is never a mentor");
                Assert.That(mentor.CanMentor(session, session), Is.False);
                Assert.That(mentor.Award(session, session, 5, "fs-mentor-reason-revive"), Is.Zero);
                Assert.That(mentor.CurrentRecord(session.UserId.UserId).Total, Is.Zero);
            });
        });
    }
}
