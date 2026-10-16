using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Ladder;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Mentor;
using Content.Server._FinalStand.Onboarding;
using Content.Server._FinalStand.Perks;
using Content.Server._FinalStand.Titles;
using Content.Server._FinalStand.Tutorial;
using Content.Server.GameTicking;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Ladder;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Tutorial;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Robust.Server.Console;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class DebugCommandTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    [Test]
    public async Task EverySubcommandRunsAndDoesWhatItSays()
    {
        var host = Server.ResolveDependency<IServerConsoleHost>();
        void Run(string cmd) => host.ExecuteCommand(ServerSession, cmd);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival), Is.True);
            Run("fsdbg");
            Run("fsdbg fresh");
            Run("fsdbg state");
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var session = ServerSession!;
            var user = session.UserId;
            var playTime = Server.ResolveDependency<PlayTimeTrackingManager>();
            Assert.That(SEntMan.System<SharedMindSystem>().TryGetMind(session, out var mindId, out _), Is.True);
            var lvl = SEntMan.GetComponent<FSPlayerLevelComponent>(mindId);

            Assert.That(playTime.GetOverallPlaytime(session), Is.EqualTo(TimeSpan.Zero));
            Assert.That(lvl.Level, Is.EqualTo(1));

            Run("fsdbg hours 25");
            Assert.That(SEntMan.System<FSOnboardingSystem>().IsRookie(session), Is.False);
            Run("fsdbg hours 2");
            Assert.That(SEntMan.System<FSOnboardingSystem>().IsRookie(session), Is.True);

            Run("fsdbg waves 5");
            Assert.That(SEntMan.System<FSRoundPresenceSystem>().GetWavesPresent(user), Is.EqualTo(5));

            Run("fsdbg level 15");
            Assert.That(lvl.Level, Is.EqualTo(15));
            var xp = lvl.Experience;
            Run("fsdbg xp 100");
            Assert.That(lvl.Experience, Is.GreaterThan(xp));

            Run("fsdbg title FSTitleMentor");
            Assert.That(SEntMan.System<FSTitleSystem>().GetOwned(user).Select(t => t.Id), Does.Contain("FSTitleMentor"));

            var perk = Content.Shared._FinalStand.Perks.FSPerkDef.All.Keys.First();
            Run($"fsdbg mastery {perk} 500");
            Assert.That(SEntMan.System<FSPerkMasterySystem>().GetMastery(user)[perk], Is.EqualTo(500));

            Run("fsdbg mentor 60");
            Assert.That(SEntMan.System<FSMentorSystem>().CurrentRecord(user.UserId).Balance, Is.EqualTo(60));

            Run("fsdbg tip all");
            Run("fsdbg tipsreset");
            Assert.That(SEntMan.System<FSNewPlayerTipSystem>().HasSeen(user, FSTutorialTip.Welcome), Is.False);

            Run("fsdbg weekly 2026-10-19 00:00");
            Run("fsdbg weekly");

            Run("fsdbg clearladder");
            Run("fsdbg seed 12 Hard 3");
            Assert.That(SEntMan.System<FSLadderSystem>().GetTopRuns(FSDifficulty.Hard, FSPlayerBracket.Squad), Is.Not.Empty);
            Run("fsdbg seed 3 Normal 1 weekly");

            Run("fsdbg special FSZombieBloater");
            Assert.That(SEntMan.System<FSNewPlayerTipSystem>().HasSeen(user, FSTutorialTip.Bloater), Is.True);

            Run("fsdbg objective random");
            Run("fsdbg objective complete");
            Run("fsdbg firstround");
            Run("fsdbg discord");
            Run("fsdbg state");

            Run("fsdbg down");
            Assert.That(SEntMan.System<MobStateSystem>().IsCritical(session.AttachedEntity!.Value), Is.True);
        });
    }
}
