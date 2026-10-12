using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Records;
using Content.Server._FinalStand.Titles;
using Content.Server.GameTicking;
using Content.Server.Voting.Managers;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.Mind;
using Robust.Server.Console;
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
            Assert.That(SEntMan.System<GameTicker>().StartGameRule("WaveSurvivalZombie"), Is.True);
            Assert.That(Server.ResolveDependency<IVoteManager>().ActiveVotes
                .Any(v => v.Title == Loc.GetString("fs-difficulty-vote-title")), Is.False);
        });
    }

    [Test]
    public async Task DifficultyScalesTheWaveAndLocksWhenWaveOneStarts()
    {
        await Server.WaitAssertion(() =>
        {
            var ticker = SEntMan.System<GameTicker>();
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            var shell = Server.ResolveDependency<IServerConsoleHost>().LocalShell;

            Assert.That(waveRule.TrySetDifficulty(FSDifficulty.Hard), Is.False, "no active round, nothing to set");
            Assert.That(ticker.StartGameRule("WaveSurvivalZombie"), Is.True);
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

            waveRule.ForceNextWave(shell);
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
            Assert.That(ticker.StartGameRule("WaveSurvivalZombie"), Is.True);

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
            var shell = Server.ResolveDependency<IServerConsoleHost>().LocalShell;
            Assert.That(ticker.StartGameRule("WaveSurvivalZombie"), Is.True);

            var vote = votes.ActiveVotes.Single(v => v.Title == Loc.GetString("fs-difficulty-vote-title"));
            vote.CastVote(ServerSession!, (int) FSDifficulty.Hard);
            waveRule.ForceNextWave(shell);

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
            var shell = Server.ResolveDependency<IServerConsoleHost>().LocalShell;
            var session = ServerSession!;

            store.DeleteProgress(session.UserId.UserId);
            if (!minds.TryGetMind(session, out var mindId, out _))
            {
                var body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                mindId = minds.CreateMind(session.UserId, "Defender");
                minds.TransferTo(mindId, body, mind: SEntMan.GetComponent<MindComponent>(mindId));
            }
            var wallet = SEntMan.EnsureComponent<FSPlayerWalletComponent>(mindId);

            Assert.That(ticker.StartGameRule("WaveSurvivalZombie"), Is.True);
            Assert.That(waveRule.TrySetDifficulty(FSDifficulty.Nightmare), Is.True);
            waveRule.ForceNextWave(shell, 30);
            var before = wallet.PerkPoints;
            waveRule.ForceNextWave(shell);

            var owned = titles.GetOwned(session.UserId).Select(t => t.Id).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(wallet.PerkPoints - before,
                    Is.GreaterThanOrEqualTo(5 + FSDifficultyRules.VictoryBonusPerkPoints(FSDifficulty.Nightmare)));
                Assert.That(owned, Does.Contain(FSRecordsSystem.VictoryTitle));
                Assert.That(owned, Does.Contain(FSRecordsSystem.HardVictoryTitle));
                Assert.That(owned, Does.Contain(FSRecordsSystem.NightmareVictoryTitle));
            });
        });
    }
}
