using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Records;
using Content.Server._FinalStand.Titles;
using Content.Server.GameTicking;
using Content.Server.Voting.Managers;
using Content.Shared._FinalStand.CCC;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class DifficultyTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    [Test]
    public async Task NoVoteOpensWhenTheVoteIsTurnedOff()
    {
        await OverrideCVar(Side.Server, CCVars.FSDifficultyVoteSeconds, 0);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival), Is.True);
            Assert.That(Server.ResolveDependency<IVoteManager>().ActiveVotes
                .Any(v => v.Title == Loc.GetString("fs-difficulty-vote-title")), Is.False);
        });
    }

    [Test]
    public async Task AnOpenConsoleFollowsTheDifficultyVote()
    {
        var map = await Pair.CreateTestMap();
        var ccc = EntityUid.Invalid;
        var nightmareTotal = 0;
        await Server.WaitAssertion(() =>
        {
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            var minds = SEntMan.System<SharedMindSystem>();
            Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival), Is.True);
            Assert.That(waveRule.TrySetDifficulty(FSDifficulty.Normal), Is.True);

            var body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var mindId = minds.CreateMind(ServerSession!.UserId, "Captain");
            minds.TransferTo(mindId, body, mind: SEntMan.GetComponent<MindComponent>(mindId));
            ccc = SEntMan.SpawnEntity("FinalStandCCC", map.GridCoords);
            SEntMan.System<SharedUserInterfaceSystem>().OpenUi(ccc, CCCUiKey.Key, body);
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            var ui = SEntMan.System<SharedUserInterfaceSystem>();
            Assert.That(ui.TryGetUiState<CCCBoundUserInterfaceState>(ccc, CCCUiKey.Key, out var shown), Is.True);
            Assert.That(shown!.EstimatedEnemyCount, Is.EqualTo(waveRule.GetEnemyTotal()), "test precondition");

            Assert.That(waveRule.TrySetDifficulty(FSDifficulty.Nightmare), Is.True);
            nightmareTotal = waveRule.GetEnemyTotal();
            Assert.That(nightmareTotal, Is.GreaterThan(shown.EstimatedEnemyCount), "test precondition");
        });
        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedUserInterfaceSystem>()
                    .TryGetUiState<CCCBoundUserInterfaceState>(ccc, CCCUiKey.Key, out var shown), Is.True);
            Assert.That(shown!.EstimatedEnemyCount, Is.EqualTo(nightmareTotal),
                "the console shows what the vote picked without being reopened");
        });
    }

    [Test]
    public async Task AnAdminPickEndsTheDifficultyVote()
    {
        await OverrideCVar(Side.Server, CCVars.FSDifficultyVoteSeconds, 60);
        bool VoteOpen() => Server.ResolveDependency<IVoteManager>().ActiveVotes
            .Any(v => v.Title == Loc.GetString("fs-difficulty-vote-title"));

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival), Is.True);
            Assert.That(VoteOpen(), Is.True, "test precondition");
            Assert.That(SEntMan.System<WaveGameRuleSystem>().TrySetDifficulty(FSDifficulty.Hard), Is.True);
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(VoteOpen(), Is.False, "the vote would overwrite the admin's pick when it ends");
            Assert.That(SEntMan.System<WaveGameRuleSystem>().GetDifficulty(), Is.EqualTo(FSDifficulty.Hard));
        });
    }

    [Test]
    public async Task DifficultyScalesTheWaveAndLocksWhenWaveOneStarts()
    {
        await Server.WaitAssertion(() =>
        {
            var ticker = SEntMan.System<GameTicker>();
            var waveRule = SEntMan.System<WaveGameRuleSystem>();

            Assert.That(waveRule.TrySetDifficulty(FSDifficulty.Hard), Is.False, "no active round, nothing to set");
            Assert.That(ticker.StartGameRule(FSTestRules.WaveSurvival), Is.True);
            Assert.That(waveRule.GetDifficulty(), Is.EqualTo(FSDifficulty.Normal));

            Assert.That(waveRule.TrySetDifficulty(FSDifficulty.Normal), Is.True);
            Assert.That(waveRule.TryGetActiveState(out var normal), Is.True);

            Assert.That(waveRule.TrySetDifficulty(FSDifficulty.Nightmare), Is.True);
            Assert.That(waveRule.TryGetActiveState(out var nightmare), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(waveRule.GetDifficulty(), Is.EqualTo(FSDifficulty.Nightmare));
                Assert.That(nightmare.TotalEnemies, Is.EqualTo((int) (normal.TotalEnemies * FSDifficultyRules.CountMultiplier(FSDifficulty.Nightmare))).Within(1),
                    "Nightmare brings half again as many enemies");
                Assert.That(waveRule.GetRewardMultiplier(), Is.EqualTo(FSDifficultyRules.RewardMultiplier(FSDifficulty.Nightmare)));
            });

            waveRule.ForceNextWave();
            Assert.That(waveRule.TrySetDifficulty(FSDifficulty.Normal), Is.False,
                "changing difficulty mid-round would let a team farm Nightmare rewards on Normal");
            Assert.That(waveRule.GetDifficulty(), Is.EqualTo(FSDifficulty.Nightmare));
        });
    }

    [Test]
    public async Task TheRoundStartVotePicksTheDifficulty()
    {
        await OverrideCVar(Side.Server, CCVars.FSDifficultyVoteSeconds, 1);

        await Server.WaitAssertion(() =>
        {
            var ticker = SEntMan.System<GameTicker>();
            var votes = Server.ResolveDependency<IVoteManager>();
            Assert.That(ticker.StartGameRule(FSTestRules.WaveSurvival), Is.True);

            var vote = votes.ActiveVotes.Single(v => v.Title == Loc.GetString("fs-difficulty-vote-title"));
            vote.CastVote(ServerSession!, (int) FSDifficulty.Nightmare);
        });

        var timing = Server.ResolveDependency<IGameTiming>();
        var deadline = timing.RealTime + TimeSpan.FromSeconds(20);
        while (SEntMan.System<WaveGameRuleSystem>().GetDifficulty() != FSDifficulty.Nightmare && timing.RealTime < deadline)
            await RunTicksSync(10);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<WaveGameRuleSystem>().GetDifficulty(), Is.EqualTo(FSDifficulty.Nightmare),
                "the winning option becomes the round's difficulty");
        });
    }

    [Test]
    public async Task StartingWaveOneClosesTheVote()
    {
        await OverrideCVar(Side.Server, CCVars.FSDifficultyVoteSeconds, 60);

        await Server.WaitAssertion(() =>
        {
            var ticker = SEntMan.System<GameTicker>();
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            var votes = Server.ResolveDependency<IVoteManager>();
            Assert.That(ticker.StartGameRule(FSTestRules.WaveSurvival), Is.True);

            var vote = votes.ActiveVotes.Single(v => v.Title == Loc.GetString("fs-difficulty-vote-title"));
            vote.CastVote(ServerSession!, (int) FSDifficulty.Hard);
            waveRule.ForceNextWave();

            Assert.Multiple(() =>
            {
                Assert.That(vote.Cancelled, Is.True, "a vote that can no longer change anything should not stay on screen");
                Assert.That(waveRule.GetDifficulty(), Is.EqualTo(FSDifficulty.Normal));
            });
        });
    }

    [Test]
    public async Task ANightmareVictoryPaysTheBonusAndBothTitles()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var ticker = SEntMan.System<GameTicker>();
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            var titles = SEntMan.System<FSTitleSystem>();
            var minds = SEntMan.System<SharedMindSystem>();
            var store = SEntMan.System<FSPlayerDataStore>();
            var session = ServerSession!;

            store.DeleteProgress(session.UserId.UserId);
            if (!minds.TryGetMind(session, out var mindId, out _))
            {
                var body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                mindId = minds.CreateMind(session.UserId, "Defender");
                minds.TransferTo(mindId, body, mind: SEntMan.GetComponent<MindComponent>(mindId));
            }
            var wallet = SEntMan.EnsureComponent<FSPlayerWalletComponent>(mindId);

            Assert.That(ticker.StartGameRule(FSTestRules.WaveSurvival), Is.True);
            SEntMan.EnsureComponent<FSPlayerLevelComponent>(mindId);
            var present = new WaveEndedEvent(1);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref present);
            Assert.That(waveRule.TrySetDifficulty(FSDifficulty.Nightmare), Is.True);
            waveRule.ForceNextWave(30);
            SEntMan.RemoveComponent<FSPlayerLevelComponent>(mindId);
            var before = wallet.PerkPoints;
            waveRule.ForceNextWave();

            var owned = titles.GetOwned(session.UserId).Select(t => t.Id).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(wallet.PerkPoints - before,
                    Is.EqualTo(5 + FSDifficultyRules.VictoryBonusPerkPoints(FSDifficulty.Nightmare)));
                Assert.That(owned, Does.Contain(FSRecordsSystem.VictoryTitle.Id));
                Assert.That(owned, Does.Contain(FSRecordsSystem.HardVictoryTitle.Id));
                Assert.That(owned, Does.Contain(FSRecordsSystem.NightmareVictoryTitle.Id));
            });
        });
    }
}
