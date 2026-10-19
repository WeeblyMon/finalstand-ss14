using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

// The HUD reads wave state from a networked component on the rule entity, so every client has to get it.
[TestFixture]
[TestOf(typeof(FSWaveStateComponent))]
public sealed class WaveStateSyncTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        Connected = true,
        DummyTicker = false,
        Map = PoolManager.TestStation,
    };

    private static readonly EntProtoId WaveRule = "WaveSurvivalZombie";

    [Test]
    public async Task ClientSeesPhaseChanges()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();
        var waves = sEntMan.System<WaveGameRuleSystem>();
        var ticker = sEntMan.System<GameTicker>();
        var timing = server.ResolveDependency<IGameTiming>();
        var cTiming = client.ResolveDependency<IGameTiming>();

        await server.WaitPost(() => Assert.That(ticker.StartGameRule(WaveRule, out _)));
        await Pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            Assert.That(TryGetState(cEntMan, out var state), "the client never received the wave state");
            Assert.That(state.Phase, Is.EqualTo(WavePhase.Prep));
            Assert.That(state.PhaseEndsAt, Is.GreaterThan(cTiming.CurTime), "prep should count down to a future time");
        });

        await server.WaitPost(() => waves.RequestWaveStart());
        await Pair.RunTicksSync(timing.TickRate * 11);

        await client.WaitAssertion(() =>
        {
            Assert.That(TryGetState(cEntMan, out var state));
            Assert.That(state.Phase, Is.EqualTo(WavePhase.Combat));
            Assert.That(state.Wave, Is.EqualTo(1));
            Assert.That(state.EnemiesTotal, Is.GreaterThan(0));
        });
    }

    private static bool TryGetState(IEntityManager entMan, out FSWaveStateComponent state)
    {
        var query = entMan.EntityQueryEnumerator<FSWaveStateComponent>();
        return query.MoveNext(out state!);
    }
}
