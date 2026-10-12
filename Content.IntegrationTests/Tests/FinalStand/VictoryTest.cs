using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Records;
using Content.Server._FinalStand.Titles;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Server.Console;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class VictoryTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    [Test]
    public void OnlyTheVictoryWaveOrLaterWins()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WaveGameRuleSystem.IsVictoryWave(29, 30), Is.False);
            Assert.That(WaveGameRuleSystem.IsVictoryWave(30, 30), Is.True);
            Assert.That(WaveGameRuleSystem.IsVictoryWave(31, 30), Is.True,
                "an admin jumping past 30 must still be able to finish the round");
            Assert.That(WaveGameRuleSystem.IsVictoryWave(500, 0), Is.False, "0 turns victory off");
        });
    }

    [Test]
    public async Task ClearingWaveThirtyWinsTheRoundAndPaysEveryone()
    {
        var map = await Pair.CreateTestMap();
        var afterVictory = 0;
        var walletOwner = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            var ticker = SEntMan.System<GameTicker>();
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            var records = SEntMan.System<FSRecordsSystem>();
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
            walletOwner = mindId;
            var pointsBefore = wallet.PerkPoints;

            Assert.That(ticker.StartGameRule(FSTestRules.WaveSurvival), Is.True);

            waveRule.ForceNextWave(shell, 30);
            Assert.That(waveRule.TryGetWaveStatus(out var wave, out var phase), Is.True);
            Assert.That((wave, phase), Is.EqualTo((30, WavePhase.Combat)));
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound), "nothing ends the round before wave 30 falls");

            waveRule.ForceNextWave(shell);

            Assert.Multiple(() =>
            {
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound), "clearing wave 30 ends the round in victory");
                Assert.That(waveRule.GetWaveNumber(), Is.EqualTo(30), "the wave counter must not roll into an endless wave 31");
                Assert.That(wallet.PerkPoints - pointsBefore, Is.GreaterThanOrEqualTo(5),
                    "every defender earns the victory PP (round-end XP can add level-up points on top)");
                Assert.That(records.VictoryThisRound, Is.True);
                Assert.That(titles.GetOwned(session.UserId).Select(t => t.Id), Does.Contain(FSRecordsSystem.VictoryTitle));
            });

            afterVictory = wallet.PerkPoints;
        });

        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            Assert.That(waveRule.TryGetWaveStatus(out var wave, out _), Is.True);
            Assert.That(wave, Is.EqualTo(30), "a won round stops ticking: no prep timer, no wave 31");
            Assert.That(SEntMan.GetComponent<FSPlayerWalletComponent>(walletOwner).PerkPoints, Is.EqualTo(afterVictory),
                "victory pays once");
        });
    }
}
