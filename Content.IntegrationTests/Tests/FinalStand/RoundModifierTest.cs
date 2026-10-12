using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.Damage.Components;
using Robust.Server.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class RoundModifierTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    [Test]
    public async Task RollsNeverPairIncompatibleModifiers()
    {
        await Server.WaitAssertion(() =>
        {
            var pool = SProtoMan.EnumeratePrototypes<FSRoundModifierPrototype>().ToList();
            Assert.That(pool, Has.Count.GreaterThanOrEqualTo(6));

            var random = new System.Random(1234);
            for (var i = 0; i < 500; i++)
            {
                var rolled = FSRoundModifierRules.Roll(pool, 3, () => (float) random.NextDouble());
                Assert.That(rolled.Select(m => m.ID), Is.Unique);
                foreach (var a in rolled)
                {
                    foreach (var b in rolled.Where(b => b != a))
                        Assert.That(FSRoundModifierRules.Compatible(a, b), Is.True, $"{a.ID} rolled with {b.ID}");
                }
            }

            Assert.That(FSRoundModifierRules.Roll(pool, 0, () => 0.5f), Is.Empty);
            Assert.That(FSRoundModifierRules.RewardMultiplier(pool), Is.EqualTo(1f + FSRoundModifierRules.MaxRewardBonus),
                "stacking every modifier is capped at double rewards");
        });
    }

    [Test]
    public async Task SwarmDoublesTheWaveAndRaisesRewards()
    {
        await Server.WaitAssertion(() =>
        {
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            Assert.That(SEntMan.System<GameTicker>().StartGameRule("WaveSurvivalZombie"), Is.True);

            Assert.That(waveRule.TrySetModifiers(new List<ProtoId<FSRoundModifierPrototype>>()), Is.True);
            Assert.That(waveRule.TryGetActiveState(out var plain), Is.True);
            var plainReward = waveRule.GetRewardMultiplier();

            Assert.That(waveRule.TrySetModifiers(new List<ProtoId<FSRoundModifierPrototype>> { "FSModifierSwarm" }), Is.True);
            Assert.That(waveRule.TryGetActiveState(out var swarm), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(swarm.TotalEnemies, Is.EqualTo(plain.TotalEnemies * 2).Within(1));
                Assert.That(waveRule.GetRewardMultiplier(), Is.EqualTo(plainReward * 1.15f).Within(0.001f));
                Assert.That(waveRule.GetActiveModifiers().Select(m => m.ID), Is.EqualTo(new[] { "FSModifierSwarm" }));
            });

            Assert.That(waveRule.TrySetModifiers(new List<ProtoId<FSRoundModifierPrototype>> { "FSModifierSwarm", "FSModifierJuggernauts" }),
                Is.False, "conflicting modifiers must be refused");
        });
    }

    [Test]
    public async Task ModifierComponentsReachEverySpawnedEnemy()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            var spawning = SEntMan.System<WaveEnemySpawningSystem>();
            Assert.That(SEntMan.System<GameTicker>().StartGameRule("WaveSurvivalZombie"), Is.True);
            Assert.That(waveRule.TrySetModifiers(new List<ProtoId<FSRoundModifierPrototype>> { "FSModifierRegenerators" }), Is.True);

            var rule = SEntMan.EntityQuery<WaveGameRuleComponent>().Single();
            var zombie = spawning.SpawnWaveEnemy("FSZombieNormal", map.GridCoords, rule);

            Assert.That(SEntMan.TryGetComponent<PassiveDamageComponent>(zombie, out var regen), Is.True);
            Assert.That(regen!.Damage.GetTotal().Float(), Is.LessThan(0f), "regenerators heal, they don't hurt");
        });
    }

    [Test]
    public async Task TheRerollVoteRunsOncePerRoundBeforeWaveOne()
    {
        await Server.WaitAssertion(() =>
        {
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            var shell = Server.ResolveDependency<IServerConsoleHost>().LocalShell;

            Assert.That(waveRule.TryStartRerollVote(null, out _), Is.False, "no round, no vote");
            Assert.That(SEntMan.System<GameTicker>().StartGameRule("WaveSurvivalZombie"), Is.True);

            Assert.That(waveRule.TryStartRerollVote(ServerSession, out var error), Is.True, error);
            Assert.That(waveRule.TryStartRerollVote(ServerSession, out _), Is.False, "one reroll vote at a time");

            waveRule.ForceNextWave(shell);
            Assert.That(waveRule.TryStartRerollVote(ServerSession, out _), Is.False, "modifiers lock with wave 1");
            Assert.That(waveRule.TrySetModifiers(new List<ProtoId<FSRoundModifierPrototype>>()), Is.False);
        });
    }
}
