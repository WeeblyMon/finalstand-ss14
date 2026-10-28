#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class AutoStartTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        InLobby = true,
        Map = "FinalStandMap1",
    };

    // Release servers run the packaged server_config.toml, which auto-calls the evac shuttle 90 minutes in.
    // Final Stand has no evac and the countdown ends the round, so auto-start has to switch it off.
    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.FSAutoStart), true)]
    public async Task ALongRoundNeverCallsTheEvacShuttle()
    {
        var ticker = SEntMan.System<GameTicker>();
        var roundEnd = SEntMan.System<RoundEndSystem>();
        var timing = Server.ResolveDependency<IGameTiming>();

        // What server_config.toml ships with.
        await OverrideCVar(Side.Server, CCVars.EmergencyShuttleAutoCallTime, 90);
        await OverrideCVar(Side.Server, CCVars.EmergencyShuttleEnabled, true);

        // Back through the lobby, where auto-start applies its settings.
        await Server.WaitPost(() => ticker.RestartRound());
        await Pair.RunTicksSync(5);
        await Server.WaitPost(() => ticker.StartRound(force: true));
        await Pair.RunTicksSync(10);

        await Server.WaitPost(() => roundEnd.AutoCallStartTime = timing.CurTime - TimeSpan.FromHours(3));
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(roundEnd.ExpectedCountdownEnd, Is.Null, "the evac shuttle was auto-called");
        });
    }
}
