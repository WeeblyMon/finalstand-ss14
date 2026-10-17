using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
[TestOf(typeof(WaveGameRuleSystem))]
public sealed class WaveLifecycleTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Map = PoolManager.TestStation,
    };

    private static readonly EntProtoId WaveRule = "WaveSurvivalZombie";

    // Prep -> requested start -> countdown -> combat -> CCC life lost -> prep again -> rule ended.
    [Test]
    public async Task WaveRuleRunsThroughItsPhases()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var ticker = entMan.System<GameTicker>();
        var waves = entMan.System<WaveGameRuleSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid rule = default;
        await server.WaitPost(() =>
        {
            Assert.That(ticker.StartGameRule(WaveRule, out rule));
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(waves.TryGetWaveStatus(out var wave, out var phase));
            Assert.That(wave, Is.EqualTo(1));
            Assert.That(phase, Is.EqualTo(WavePhase.Prep));

            entMan.EventBus.RaiseEvent(EventSource.Local, new WaveStartRequestEvent());
            Assert.That(entMan.GetComponent<WaveGameRuleComponent>(rule).VoteCountdownActive);
        });

        await Pair.RunTicksSync(timing.TickRate * 11);

        await server.WaitAssertion(() =>
        {
            Assert.That(waves.TryGetWaveStatus(out var wave, out var phase));
            Assert.That(phase, Is.EqualTo(WavePhase.Combat));
            Assert.That(wave, Is.EqualTo(1));

            Assert.That(waves.FailWave(), Is.EqualTo(1));
            Assert.That(waves.TryGetWaveStatus(out wave, out phase));
            Assert.That(phase, Is.EqualTo(WavePhase.Prep), "losing a CCC life sends the round back to prep");
            Assert.That(wave, Is.EqualTo(1), "the failed wave is replayed, not skipped");
        });

        await Pair.RunTicksSync(timing.TickRate * 2);

        await server.WaitPost(() =>
        {
            ticker.EndGameRule(rule);
        });

        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(waves.TryGetWaveStatus(out _, out _), Is.False);
        });
    }
}
