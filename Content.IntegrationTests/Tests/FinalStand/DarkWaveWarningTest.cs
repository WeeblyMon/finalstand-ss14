using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

// A dark wave is announced 30 s into prep. Starting the wave sooner (the Captain, or most of the crew readying up)
// has to bring the warning forward, or the station blacks out with no notice.
[TestFixture]
[TestOf(typeof(FSDarkWaveSystem))]
public sealed class DarkWaveWarningTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Map = PoolManager.TestStation,
    };

    private static readonly EntProtoId WaveRule = "WaveSurvivalZombie";

    [Test]
    public async Task EarlyStartStillWarns()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var ticker = entMan.System<GameTicker>();
        var waves = entMan.System<WaveGameRuleSystem>();
        var darkWaves = entMan.System<FSDarkWaveSystem>();

        EntityUid rule = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.StartGameRule(WaveRule, out rule));

            // Armed the way a prep roll arms it: the warning waits for its delay.
            Assert.That(darkWaves.TryForce(out _));
            darkWaves.Roll(rule, 1, false);
            Assert.That(entMan.GetComponent<FSDarkWaveRuleComponent>(rule).WarningAt, Is.Not.Null);

            waves.RequestWaveStart();
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<FSDarkWaveRuleComponent>(rule);
            Assert.That(comp.Upcoming, "the dark wave stopped being upcoming before combat");
            Assert.That(comp.WarningAt, Is.Null, "the countdown started without the dark wave warning going out");
        });
    }
}
