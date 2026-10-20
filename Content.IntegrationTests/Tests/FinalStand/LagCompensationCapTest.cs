using Content.IntegrationTests.Fixtures;
using Content.Server.Movement.Systems;
using Content.Shared.CCVar;
using Robust.Client.Timing;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

// The rewind cap stops a client claiming an older view than it has, but it has to cover what an honest client
// really sees: its state buffer, the prediction run-ahead for its round trip and the tick bias.
[TestFixture]
[TestOf(typeof(LagCompensationSystem))]
public sealed class LagCompensationCapTest : GameTest
{
    // Destructive: the tests change the client's replicated net cvars, which recycling a pair doesn't reset.
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Destructive = true,
    };

    [Test]
    public async Task RewindCoversAnHonestViewButNotAnOlderClaim()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var lag = server.ResolveDependency<IEntityManager>().System<LagCompensationSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var cTiming = client.ResolveDependency<IClientGameTiming>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var cCfg = client.ResolveDependency<IConfigurationManager>();

        // Test clients run without a state buffer; use the default one so the view trails like a real client's.
        var bufferSize = 0;
        var interp = false;
        await client.WaitPost(() =>
        {
            cCfg.SetCVar(CVars.NetBufferSize, 2);
            bufferSize = cCfg.GetCVar(CVars.NetBufferSize);
            interp = cCfg.GetCVar(CVars.NetInterp);
        });

        await Pair.RunTicksSync(30);

        uint clientLead = 0;
        await client.WaitAssertion(() => clientLead = cTiming.CurTick.Value - cTiming.LastRealTick.Value);

        await server.WaitAssertion(() =>
        {
            var session = Pair.Player!;
            var tickRate = timing.TickRate;
            var maxRewind = cfg.GetCVar(CCVars.FSLagCompensationMaxRewindMs) / 1000.0 * tickRate;
            var slack = cfg.GetCVar(CCVars.FSLagCompensationRewindSlackMs) / 1000.0 * tickRate;

            // Its buffered states, the run-ahead for its round trip (Windows lag bias at worst) and tick bias 1.
            var honest = (uint) ((interp ? 2 : 1) + bufferSize)
                         + (uint) Math.Ceiling(tickRate * (session.Ping / 1000.0 + 0.016)) + 1;

            Assert.Multiple(() =>
            {
                lag.SetViewTick(session, new GameTick(timing.CurTick.Value - honest));
                Assert.That(lag.GetRewindTicks(session), Is.EqualTo(honest + 0.5), "an honest view was clipped");

                lag.SetViewTick(session, new GameTick(timing.CurTick.Value - clientLead));
                Assert.That(lag.GetRewindTicks(session), Is.EqualTo(clientLead + 0.5),
                    "the paired client's own prediction lead was clipped");

                lag.SetViewTick(session, new GameTick(timing.CurTick.Value - honest - 10));
                Assert.That(lag.GetRewindTicks(session), Is.EqualTo(Math.Min(maxRewind, honest + 0.5 + slack)).Within(1e-9),
                    "an older claimed view wasn't capped");
            });
        });
    }

    // net.buffer_size is replicated as the client sends it, so an absurd value has to neither throw nor lift the cap.
    [Test]
    public async Task AnAbsurdClientBufferStaysCapped()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var lag = server.ResolveDependency<IEntityManager>().System<LagCompensationSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var cCfg = client.ResolveDependency<IConfigurationManager>();

        await client.WaitPost(() => cCfg.SetCVar(CVars.NetBufferSize, int.MaxValue));
        await Pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var session = Pair.Player!;
            var tickRate = timing.TickRate;
            var slack = cfg.GetCVar(CCVars.FSLagCompensationRewindSlackMs) / 1000.0 * tickRate;

            // Interpolation on plus the most buffer trusted, the run-ahead and tick bias 1.
            var trusted = 2 + 4 + Math.Ceiling(tickRate * (session.Ping / 1000.0 + 0.016)) + 1 + 0.5;

            lag.SetViewTick(session, new GameTick(timing.CurTick.Value - 14));
            var rewind = 0.0;
            Assert.DoesNotThrow(() => rewind = lag.GetRewindTicks(session));
            Assert.That(rewind, Is.EqualTo(Math.Min(14.5, trusted + slack)).Within(1e-9));
        });
    }
}
