using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.Economy;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.FinalStand;

// A money upgrade marks what its wielder hits; only that player's kill collects the bonus, on top of the plain kill pay.
[TestFixture]
public sealed class KillBonusPayeeTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    [Test]
    public async Task OnlyTheMarkingPlayerCollectsTheBonus()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var minds = SEntMan.System<SharedMindSystem>();
            var mobState = SEntMan.System<MobStateSystem>();
            var bonus = SEntMan.System<MoneyGainBonusUpgradeSystem>();
            var rewards = SEntMan.System<FSWaveRewardsSystem>();

            if (!minds.TryGetMind(ServerSession!, out var mindId, out var mind) || mind.OwnedEntity is not { } body)
            {
                body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                mindId = minds.CreateMind(ServerSession!.UserId, "Defender");
                minds.TransferTo(mindId, body, mind: SEntMan.GetComponent<MindComponent>(mindId));
            }

            var other = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var otherMind = minds.CreateMind(null, "Other");
            minds.TransferTo(otherMind, other, mind: SEntMan.GetComponent<MindComponent>(otherMind));

            var wallet = SEntMan.EnsureComponent<FSPlayerWalletComponent>(mindId);
            var otherWallet = SEntMan.EnsureComponent<FSPlayerWalletComponent>(otherMind);
            Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival), Is.True);
            SEntMan.System<WaveGameRuleSystem>().ForceNextWave(6);

            EntityUid Enemy()
            {
                var enemy = SEntMan.SpawnEntity("FSZombieNormal", new EntityCoordinates(map.Grid, 3.5f, 0.5f));
                SEntMan.EnsureComponent<WaveSpawnedTagComponent>(enemy);
                return enemy;
            }

            var plain = Enemy();
            var kill = rewards.KillCredits(plain);
            Assert.That(kill, Is.GreaterThan(0));
            var before = wallet.Credits;
            mobState.ChangeMobState(plain, MobState.Dead, origin: body);
            var plainPay = wallet.Credits - before;

            var marked = Enemy();
            bonus.Mark(marked, body, 10, 0.5f);
            before = wallet.Credits;
            mobState.ChangeMobState(marked, MobState.Dead, origin: body);
            Assert.That(wallet.Credits - before - plainPay, Is.EqualTo(10 + (int) MathF.Round(0.5f * kill)),
                "the marking player's kill pays the flat bonus plus the share of the kill");

            var otherBefore = otherWallet.Credits;
            mobState.ChangeMobState(Enemy(), MobState.Dead, origin: other);
            var otherPlainPay = otherWallet.Credits - otherBefore;

            var stolen = Enemy();
            bonus.Mark(stolen, body, 10, 0.5f);
            before = wallet.Credits;
            otherBefore = otherWallet.Credits;
            mobState.ChangeMobState(stolen, MobState.Dead, origin: other);
            Assert.That(wallet.Credits, Is.EqualTo(before), "someone else's kill pays the marker nothing");
            Assert.That(otherWallet.Credits - otherBefore, Is.EqualTo(otherPlainPay), "and the killer only the plain kill");
        });
    }
}
