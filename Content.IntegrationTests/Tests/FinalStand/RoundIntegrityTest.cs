#pragma warning disable RA0002
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Ladder;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Onboarding;
using Content.Server.GameTicking;
using Content.Server.Voting.Managers;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Server.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class RoundIntegrityTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    private EntityUid EnsurePlayer(Robust.Shared.Map.EntityCoordinates coords)
    {
        var minds = SEntMan.System<SharedMindSystem>();
        var session = ServerSession!;
        if (!minds.TryGetMind(session, out var mindId, out _))
        {
            var body = SEntMan.SpawnEntity("MobHuman", coords);
            mindId = minds.CreateMind(session.UserId, "Defender");
            minds.TransferTo(mindId, body, mind: SEntMan.GetComponent<MindComponent>(mindId));
        }

        SEntMan.EnsureComponent<FSPlayerWalletComponent>(mindId);
        SEntMan.EnsureComponent<FSPlayerLevelComponent>(mindId);
        return mindId;
    }

    private void SetWavesCleared(int waves)
    {
        var rule = SEntMan.EntityQuery<WaveGameRuleComponent>().Single();
        typeof(WaveGameRuleComponent).GetField(nameof(WaveGameRuleComponent.WavesCompleted))!.SetValue(rule, waves);
    }

    private FSWeeklyChallengePrototype ThisWeek()
        => SEntMan.System<FSWeeklyChallengeSystem>().GetChallenge(System.DateTime.UtcNow)!;

    private void EndWave(int wave)
    {
        var ended = new WaveEndedEvent(wave);
        SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ended);
    }

    [Test]
    public async Task AdminCommandsCannotRewriteAWeeklyRun()
    {
        await Server.WaitAssertion(() =>
        {
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival), Is.True);
            Assert.That(SEntMan.System<FSWeeklyChallengeSystem>().StartWeekly(ThisWeek()), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(waveRule.TrySetModifiers(new List<ProtoId<FSRoundModifierPrototype>>()), Is.False,
                    "setmodifiers must not strip a weekly's modifiers");
                Assert.That(waveRule.TrySetDifficulty(FSDifficulty.Normal), Is.False,
                    "setdifficulty must not soften a weekly");
            });
        });
    }

    [Test]
    public async Task ClearingTheWeeklyPaysOncePerWeek()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            SEntMan.System<FSPlayerDataStore>().DeleteProgress(ServerSession!.UserId.UserId);
            var mindId = EnsurePlayer(map.GridCoords);
            var wallet = SEntMan.GetComponent<FSPlayerWalletComponent>(mindId);
            var challenge = ThisWeek();

            Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival), Is.True);
            Assert.That(SEntMan.System<FSWeeklyChallengeSystem>().StartWeekly(challenge), Is.True);
            EndWave(1);
            SetWavesCleared(challenge.ClearWave);

            SEntMan.RemoveComponent<FSPlayerLevelComponent>(mindId);
            var before = wallet.PerkPoints;
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundEndTextAppendEvent());
            Assert.That(wallet.PerkPoints - before, Is.EqualTo(FSWeeklyRules.ClearPerkPoints));

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundEndTextAppendEvent());
            Assert.That(wallet.PerkPoints - before, Is.EqualTo(FSWeeklyRules.ClearPerkPoints), "the weekly pays once per week");
        });
    }

    [Test]
    public async Task SkippingWavesKeepsTheRunOffTheLadderAndTheWeekly()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            var store = SEntMan.System<FSPlayerDataStore>();
            var ticker = SEntMan.System<GameTicker>();
            store.DeleteProgress(ServerSession!.UserId.UserId);
            store.DeleteAllRuns();
            var mindId = EnsurePlayer(map.GridCoords);
            var wallet = SEntMan.GetComponent<FSPlayerWalletComponent>(mindId);
            var challenge = ThisWeek();

            Assert.That(ticker.StartGameRule(FSTestRules.WaveSurvival), Is.True);
            Assert.That(SEntMan.System<FSWeeklyChallengeSystem>().StartWeekly(challenge), Is.True);
            EndWave(1);
            waveRule.ForceNextWave(Server.ResolveDependency<IServerConsoleHost>().LocalShell, challenge.ClearWave + 1);
            Assert.That(waveRule.TryGetRoundSummary(out var summary), Is.True);
            Assert.That(summary.Assisted, Is.True);

            SEntMan.RemoveComponent<FSPlayerLevelComponent>(mindId);
            var before = wallet.PerkPoints;
            ticker.EndRound();
            Assert.Multiple(() =>
            {
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound));
                Assert.That(wallet.PerkPoints, Is.EqualTo(before), "a skipped-to clear earns no weekly reward");
                Assert.That(store.GetWeeklyTopRuns(summary.WeeklyKey!, 10), Is.Empty, "and never reaches the weekly ladder");
                Assert.That(store.GetTopRuns(challenge.Difficulty, FSLadder.BracketFor(1), 10), Is.Empty);
            });
        });
    }

    [Test]
    public async Task AnEndlessRunAfterAWinIsRecordedWhenTheRoundEnds()
    {
        await OverrideCVar(Side.Server, CCVars.FSEndlessVoteSeconds, 600);
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var store = SEntMan.System<FSPlayerDataStore>();
            var ticker = SEntMan.System<GameTicker>();
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            store.DeleteAllRuns();
            EnsurePlayer(map.GridCoords);

            Assert.That(ticker.StartGameRule(FSTestRules.WaveSurvival, out var ruleUid), Is.True);
            var rule = SEntMan.GetComponent<WaveGameRuleComponent>(ruleUid);
            var endCombat = typeof(WaveGameRuleSystem).GetMethod("EndCombatPhase",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

            rule.WaveNumber = rule.VictoryWave;
            SetWavesCleared(rule.VictoryWave - 1);
            List<FSRunRecord> Runs() => store.GetTopRuns(FSDifficulty.Normal, FSLadder.BracketFor(1), 10);

            endCombat.Invoke(waveRule, new object[] { ruleUid, rule, false });
            Assert.That(waveRule.IsAwaitingEndless(), Is.True, "test precondition");
            Assert.That(Runs().Select(r => r.Waves), Is.EqualTo(new[] { rule.VictoryWave }),
                "the win shows on the boards straight away, before the round ends");

            Assert.That(waveRule.TryStartEndless(), Is.True);
            endCombat.Invoke(waveRule, new object[] { ruleUid, rule, false });
            Assert.That(Runs().Select(r => r.Waves), Is.EqualTo(new[] { rule.VictoryWave + 1 }),
                "each endless wave updates the same entry");

            ticker.EndRound();
            var runs = Runs();
            Assert.That(runs, Has.Count.EqualTo(1), "the endless run reaches the ladder");
            Assert.That(runs[0].Victory, Is.True);
            Assert.That(runs[0].Waves, Is.EqualTo(rule.VictoryWave + 1));
        });
    }

    [Test]
    public async Task ARunIsRecordedOnceAndOnlyWhenTheRoundEnds()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var store = SEntMan.System<FSPlayerDataStore>();
            var ticker = SEntMan.System<GameTicker>();
            store.DeleteAllRuns();
            EnsurePlayer(map.GridCoords);

            Assert.That(ticker.StartGameRule(FSTestRules.WaveSurvival), Is.True);
            EndWave(1);
            EndWave(2);
            SetWavesCleared(2);

            List<FSRunRecord> Runs() => store.GetTopRuns(FSDifficulty.Normal, FSLadder.BracketFor(1), 10);

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundEndTextAppendEvent());
            Assert.That(Runs(), Is.Empty, "a scoreboard thrown mid-round records nothing");

            ticker.EndRound();
            Assert.That(Runs(), Has.Count.EqualTo(1));

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundEndTextAppendEvent());
            Assert.That(Runs(), Has.Count.EqualTo(1), "throwing the scoreboard again doesn't record the run twice");
        });
    }

    [Test]
    public async Task TheWeeklyVoteRunsOncePerRound()
    {
        await Server.WaitAssertion(() =>
        {
            var weekly = SEntMan.System<FSWeeklyChallengeSystem>();
            var votes = Server.ResolveDependency<IVoteManager>();
            Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival), Is.True);

            Assert.That(weekly.TryStartVote(null, out var error), Is.True, error);
            foreach (var vote in votes.ActiveVotes.ToList())
                vote.Cancel();

            Assert.That(weekly.TryStartVote(null, out _), Is.False, "a failed or cancelled vote can't be restarted the same round");
        });
    }

    [Test]
    public async Task PresenceStartsOverEachRound()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var presence = SEntMan.System<FSRoundPresenceSystem>();
            var user = ServerSession!.UserId;
            EnsurePlayer(map.GridCoords);

            for (var wave = 1; wave <= 4; wave++)
                EndWave(wave);
            Assert.That(presence.GetWavesPresent(user), Is.EqualTo(4));

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Assert.That(presence.GetWavesPresent(user), Is.Zero);

            EnsurePlayer(map.GridCoords);
            EndWave(1);
            Assert.That(presence.GetWavesPresent(user), Is.EqualTo(1),
                "waves from an earlier round must not count toward this round's rewards");
        });
    }

    [Test]
    public async Task CancellingTheEndlessVoteEndsTheRoundOnAWin()
    {
        await OverrideCVar(Side.Server, CCVars.FSEndlessVoteSeconds, 600);
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var ticker = SEntMan.System<GameTicker>();
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            var shell = Server.ResolveDependency<IServerConsoleHost>().LocalShell;
            EnsurePlayer(map.GridCoords);

            Assert.That(ticker.StartGameRule(FSTestRules.WaveSurvival), Is.True);
            EndWave(1);
            waveRule.ForceNextWave(shell, 30);
            waveRule.ForceNextWave(shell);
            Assert.That(waveRule.IsAwaitingEndless(), Is.True, "test precondition");

            foreach (var vote in Server.ResolveDependency<IVoteManager>().ActiveVotes.ToList())
                vote.Cancel();

            Assert.Multiple(() =>
            {
                Assert.That(waveRule.IsAwaitingEndless(), Is.False);
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound),
                    "a cancelled vote must not leave the won round hanging forever");
            });
        });
    }

    [Test]
    public async Task OnlyPlayersWhoFoughtShareTheVictory()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var ticker = SEntMan.System<GameTicker>();
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            var titles = SEntMan.System<Content.Server._FinalStand.Titles.FSTitleSystem>();
            var shell = Server.ResolveDependency<IServerConsoleHost>().LocalShell;
            SEntMan.System<FSPlayerDataStore>().DeleteProgress(ServerSession!.UserId.UserId);
            var mindId = EnsurePlayer(map.GridCoords);
            var wallet = SEntMan.GetComponent<FSPlayerWalletComponent>(mindId);

            Assert.That(ticker.StartGameRule(FSTestRules.WaveSurvival), Is.True);
            waveRule.ForceNextWave(shell, 30);
            SEntMan.RemoveComponent<FSPlayerLevelComponent>(mindId);
            var before = wallet.PerkPoints;
            waveRule.ForceNextWave(shell);

            Assert.Multiple(() =>
            {
                Assert.That(wallet.PerkPoints, Is.EqualTo(before), "someone who wasn't present for a single wave earns no victory PP");
                Assert.That(titles.GetOwned(ServerSession!.UserId).Select(t => t.Id),
                    Does.Not.Contain(Content.Server._FinalStand.Records.FSRecordsSystem.VictoryTitle));
            });
        });
    }
}
