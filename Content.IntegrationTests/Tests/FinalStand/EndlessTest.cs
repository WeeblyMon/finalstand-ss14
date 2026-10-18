using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Records;
using Content.Server._FinalStand.Titles;
using Content.Server.GameTicking;
using Content.Server._FinalStand.Economy;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Server.Console;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class EndlessTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    [Test]
    public void TheHordeEscalatesEveryFiveWavesPastVictory()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WaveGameRuleSystem.IsEscalationWave(30, 30, 5), Is.False);
            Assert.That(WaveGameRuleSystem.IsEscalationWave(31, 30, 5), Is.False);
            Assert.That(WaveGameRuleSystem.IsEscalationWave(35, 30, 5), Is.True);
            Assert.That(WaveGameRuleSystem.IsEscalationWave(40, 30, 5), Is.True);
            Assert.That(WaveGameRuleSystem.IsEscalationWave(35, 30, 0), Is.False, "0 turns escalation off");
            Assert.That(WaveEnemyScalingSystem.GetHpMultiplier(31), Is.GreaterThan(WaveEnemyScalingSystem.GetHpMultiplier(30)),
                "enemy health keeps climbing past the victory wave");
            Assert.That(WaveEnemyScalingSystem.GetHpMultiplier(30), Is.EqualTo(9f).Within(0.001f), "no jump at the victory wave");
        });
    }

    private void EnsurePlayer(Robust.Shared.Map.EntityCoordinates coords)
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
    }

    [Test]
    public async Task VotingOnPushesPastThirtyAndEscalates()
    {
        await OverrideCVar(Side.Server, CCVars.FSEndlessVoteSeconds, 600);
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var ticker = SEntMan.System<GameTicker>();
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            var titles = SEntMan.System<FSTitleSystem>();
            var shell = Server.ResolveDependency<IServerConsoleHost>().LocalShell;
            SEntMan.System<FSPlayerDataStore>().DeleteProgress(ServerSession!.UserId.UserId);
            EnsurePlayer(map.GridCoords);

            Assert.That(ticker.StartGameRule(FSTestRules.WaveSurvival), Is.True);
            var present = new WaveEndedEvent(1);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref present);
            Assert.That(waveRule.TryStartEndless(), Is.False, "nothing to push on from before a win");

            waveRule.ForceNextWave(shell, 30);
            waveRule.ForceNextWave(shell);

            Assert.Multiple(() =>
            {
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound), "the round waits on the endless vote");
                Assert.That(waveRule.IsAwaitingEndless(), Is.True);
                Assert.That(titles.GetOwned(ServerSession!.UserId).Select(t => t.Id), Does.Contain(FSRecordsSystem.VictoryTitle),
                    "the win is banked before anyone votes");
            });

            Assert.That(waveRule.TryStartEndless(), Is.True);
            Assert.That(waveRule.TryStartEndless(), Is.False, "endless starts once");
            Assert.That(waveRule.TryGetWaveStatus(out var wave, out var phase), Is.True);
            Assert.That((wave, phase), Is.EqualTo((31, WavePhase.Prep)));
            Assert.That(waveRule.IsEndless(), Is.True);

            var modifiersBefore = waveRule.GetActiveModifiers().Count;
            waveRule.ForceNextWave(shell, 34);
            waveRule.ForceNextWave(shell);
            Assert.That(waveRule.GetWaveNumber(), Is.EqualTo(35));
            Assert.That(waveRule.GetActiveModifiers(), Has.Count.EqualTo(modifiersBefore + 1), "wave 35 adds a horde modifier");

            var ended = new WaveEndedEvent(FSRecordsSystem.EndlessTitleWave);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ended);
            Assert.That(titles.GetOwned(ServerSession!.UserId).Select(t => t.Id), Does.Contain(FSRecordsSystem.EndlessTitle));

            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound), "endless only ends when the crew falls");
        });
    }

    [Test]
    public async Task VotingNoEndsTheRoundOnAWin()
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
            var present = new WaveEndedEvent(1);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref present);
            waveRule.ForceNextWave(shell, 30);
            waveRule.ForceNextWave(shell);
            Assert.That(waveRule.IsAwaitingEndless(), Is.True);

            Assert.That(waveRule.TryDeclineEndless(), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound));
                Assert.That(waveRule.IsEndless(), Is.False);
                Assert.That(waveRule.GetWaveNumber(), Is.EqualTo(30));
            });
        });
    }
}
