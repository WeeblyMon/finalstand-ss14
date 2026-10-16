using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Onboarding;
using Content.Server._FinalStand.Titles;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Onboarding;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class OnboardingTest : GameTest
{
    private const string HumanProto = "MobHuman";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private (ICommonSession Session, EntityUid Mind, EntityUid Body) SetUpPlayer(Robust.Shared.Map.EntityCoordinates coords)
    {
        var session = ServerSession!;
        var minds = SEntMan.System<SharedMindSystem>();
        var store = SEntMan.System<FSPlayerDataStore>();

        store.DeleteProgress(session.UserId.UserId);
        store.UpsertPerkPoints(session.UserId.UserId, session.Name, 0);

        var body = SEntMan.SpawnEntity(HumanProto, coords);
        var mindId = minds.CreateMind(session.UserId, "Newcomer");
        minds.TransferTo(mindId, body, mind: SEntMan.GetComponent<MindComponent>(mindId));
        SEntMan.EnsureComponent<FSPlayerWalletComponent>(mindId);
        SEntMan.EnsureComponent<FSPlayerLevelComponent>(mindId);
        return (session, mindId, body);
    }

    private int PerkPoints(EntityUid mindId) => SEntMan.GetComponent<FSPlayerWalletComponent>(mindId).PerkPoints;

    private void AssertStillRookie(ICommonSession session)
    {
        var playTime = Server.ResolveDependency<PlayTimeTrackingManager>();
        Assert.That(playTime.GetOverallPlaytime(session), Is.LessThan(FSRookieRules.RookieThreshold),
            "test precondition: the test player must start under the rookie threshold");
    }

    [Test]
    public async Task ARookieGetsTheStarterPointsExactlyOnce()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var onboarding = SEntMan.System<FSOnboardingSystem>();
            var (session, mindId, body) = SetUpPlayer(map.GridCoords);
            AssertStillRookie(session);

            onboarding.RefreshPlayer(session, mindId, body);
            onboarding.RefreshPlayer(session, mindId, body);

            Assert.Multiple(() =>
            {
                Assert.That(PerkPoints(mindId), Is.EqualTo(FSRookieRules.StarterPerkPoints),
                    "the starter grant must not repeat on every spawn");
                Assert.That(SEntMan.GetComponent<FSPlayerBadgeComponent>(body).Rookie, Is.True);
                Assert.That(SEntMan.GetComponent<FSPlayerLevelComponent>(mindId).CatchUpMultiplier,
                    Is.EqualTo(FSRookieRules.CatchUpXpMultiplier));
            });
        });
    }

    [Test]
    public async Task ARookieEarnsDoubleWaveXp()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var onboarding = SEntMan.System<FSOnboardingSystem>();
            var (session, mindId, body) = SetUpPlayer(map.GridCoords);
            AssertStillRookie(session);
            onboarding.RefreshPlayer(session, mindId, body);

            var lvl = SEntMan.GetComponent<FSPlayerLevelComponent>(mindId);
            lvl.Level = 50;
            lvl.Experience = 0;
            lvl.XpToNextLevel = FSLevelingSystem.XpToNextLevel(50);

            var ev = new WaveEndedEvent(3);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);

            Assert.That(lvl.Experience, Is.EqualTo((int) (100 * 3 * lvl.XpMultiplier * FSRookieRules.CatchUpXpMultiplier)),
                "wave completion XP must be doubled while the player is a rookie");
        });
    }

    [Test]
    public async Task TheFirstRoundRewardNeedsFiveWavesAndPaysOnce()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var onboarding = SEntMan.System<FSOnboardingSystem>();
            var presence = SEntMan.System<FSRoundPresenceSystem>();
            var titles = SEntMan.System<FSTitleSystem>();
            var (session, mindId, body) = SetUpPlayer(map.GridCoords);
            AssertStillRookie(session);
            onboarding.RefreshPlayer(session, mindId, body);
            var afterStarter = PerkPoints(mindId);
            Assert.That(presence.GetWavesPresent(session.UserId), Is.Zero, "test precondition: no waves counted yet");

            for (var wave = 1; wave < FSRookieRules.FirstRoundMinWaves; wave++)
            {
                var ev = new WaveEndedEvent(wave);
                SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
            }

            Assert.That(presence.GetWavesPresent(session.UserId), Is.EqualTo(FSRookieRules.FirstRoundMinWaves - 1));
            Assert.That(onboarding.TryPayFirstRound(session, mindId), Is.False,
                "a rookie who left after a few waves has not finished a round");

            var last = new WaveEndedEvent(FSRookieRules.FirstRoundMinWaves);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref last);

            var levelUpPoints = PerkPoints(mindId) - afterStarter;
            Assert.That(onboarding.TryPayFirstRound(session, mindId), Is.True);
            Assert.That(onboarding.TryPayFirstRound(session, mindId), Is.False, "the first round only happens once");

            Assert.Multiple(() =>
            {
                Assert.That(PerkPoints(mindId), Is.EqualTo(afterStarter + levelUpPoints + FSRookieRules.FirstRoundPerkPoints));
                Assert.That(titles.GetOwned(session.UserId).Select(t => t.Id), Does.Contain(FSRookieRules.FirstRoundTitle));
                Assert.That(titles.GetEquipped(session.UserId)?.Id, Is.EqualTo(FSRookieRules.FirstRoundTitle),
                    "a first title is worn automatically so the player sees it");
            });
        });
    }

    [Test]
    public async Task ARookieGraduatesOnceAfterTenHours()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var onboarding = SEntMan.System<FSOnboardingSystem>();
            var titles = SEntMan.System<FSTitleSystem>();
            var playTime = Server.ResolveDependency<PlayTimeTrackingManager>();
            var (session, mindId, body) = SetUpPlayer(map.GridCoords);
            AssertStillRookie(session);

            onboarding.RefreshPlayer(session, mindId, body);
            var asRookie = PerkPoints(mindId);

            playTime.AddTimeToOverallPlaytime(session, FSRookieRules.RookieThreshold);
            onboarding.RefreshPlayer(session, mindId, body);
            onboarding.RefreshPlayer(session, mindId, body);

            Assert.Multiple(() =>
            {
                Assert.That(PerkPoints(mindId), Is.EqualTo(asRookie + FSRookieRules.GraduationPerkPoints),
                    "graduation pays once, not on every spawn after the threshold");
                Assert.That(titles.GetOwned(session.UserId).Select(t => t.Id), Does.Contain(FSRookieRules.GraduationTitle));
                Assert.That(SEntMan.GetComponent<FSPlayerBadgeComponent>(body).Rookie, Is.False);
                Assert.That(SEntMan.GetComponent<FSPlayerLevelComponent>(mindId).CatchUpMultiplier, Is.EqualTo(1f));
            });
        });
    }

    [Test]
    public async Task AVeteranSeenForTheFirstTimeGetsNoRookieRewards()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var onboarding = SEntMan.System<FSOnboardingSystem>();
            var titles = SEntMan.System<FSTitleSystem>();
            var playTime = Server.ResolveDependency<PlayTimeTrackingManager>();
            var (session, mindId, body) = SetUpPlayer(map.GridCoords);

            playTime.AddTimeToOverallPlaytime(session, FSRookieRules.RookieThreshold);
            onboarding.RefreshPlayer(session, mindId, body);

            for (var wave = 1; wave <= FSRookieRules.FirstRoundMinWaves; wave++)
            {
                var ev = new WaveEndedEvent(wave);
                SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
            }

            Assert.Multiple(() =>
            {
                Assert.That(onboarding.TryPayFirstRound(session, mindId), Is.False);
                Assert.That(titles.GetOwned(session.UserId), Is.Empty,
                    "players who were already veterans when this shipped must not get rookie or graduation titles");
                Assert.That(SEntMan.GetComponent<FSPlayerBadgeComponent>(body).Rookie, Is.False);
                Assert.That(SEntMan.GetComponent<FSPlayerLevelComponent>(mindId).CatchUpMultiplier, Is.EqualTo(1f));
            });
        });
    }

    [Test]
    public async Task TitlesMustBeOwnedToBeWorn()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var titles = SEntMan.System<FSTitleSystem>();
            var (session, _, body) = SetUpPlayer(map.GridCoords);

            Assert.That(titles.Equip(session.UserId, FSRookieRules.GraduationTitle), Is.False,
                "equipping an unearned title must be refused");
            Assert.That(titles.GetEquipped(session.UserId), Is.Null);

            Assert.That(titles.Grant(session.UserId, FSRookieRules.GraduationTitle), Is.True);
            Assert.That(titles.Grant(session.UserId, FSRookieRules.GraduationTitle), Is.False,
                "granting a title twice is a no-op");

            Assert.That(titles.TryResolve(session.UserId, "survivor", out var resolved), Is.True,
                "players type the display name, not the prototype id");
            Assert.That(resolved.Id, Is.EqualTo(FSRookieRules.GraduationTitle));
            Assert.That(SEntMan.GetComponent<FSPlayerBadgeComponent>(body).Title?.Id, Is.EqualTo(FSRookieRules.GraduationTitle));

            Assert.That(titles.Equip(session.UserId, null), Is.True);
            Assert.That(SEntMan.GetComponent<FSPlayerBadgeComponent>(body).Title, Is.Null);
        });
    }

    [Test]
    public async Task TheLeaderboardShowsRookiesAndTitles()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var onboarding = SEntMan.System<FSOnboardingSystem>();
            var titles = SEntMan.System<FSTitleSystem>();
            var leveling = SEntMan.System<FSLevelingSystem>();
            var (session, mindId, body) = SetUpPlayer(map.GridCoords);
            AssertStillRookie(session);

            onboarding.RefreshPlayer(session, mindId, body);
            titles.Grant(session.UserId, FSRookieRules.FirstRoundTitle);

            var entry = leveling.GetLeaderboardSnapshot().Single(e => e.Name == "Newcomer");
            Assert.Multiple(() =>
            {
                Assert.That(entry.Rookie, Is.True);
                Assert.That(entry.Title, Is.EqualTo(FSRookieRules.FirstRoundTitle));
            });
        });
    }
}
