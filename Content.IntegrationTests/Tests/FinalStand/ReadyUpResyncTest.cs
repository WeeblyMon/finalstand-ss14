using Content.Client._FinalStand.WaveHud;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.ReadyCheck;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.ReadyCheck;
using Content.Shared.Mobs.Components;
using Robust.Client.Graphics;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

// A client that comes back to its body mid-prep (a reconnect) starts with a blank HUD, so attaching sends it
// the ready state again.
[TestFixture]
[TestOf(typeof(FSReadyUpSystem))]
public sealed class ReadyUpResyncTest : GameTest
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
    public async Task ReattachingResendsReadyState()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cNet = client.ResolveDependency<IEntityNetworkManager>();
        var players = server.ResolveDependency<IPlayerManager>();
        var overlays = client.ResolveDependency<IOverlayManager>();
        var ticker = sEntMan.System<GameTicker>();
        var waves = sEntMan.System<WaveGameRuleSystem>();

        await server.WaitPost(() => Assert.That(ticker.StartGameRule(WaveRule, out _)));
        await Pair.RunTicksSync(10);

        EntityUid body = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(waves.IsPrep());
            Assert.That(Pair.Player!.AttachedEntity, Is.Not.Null, "the test player has no body");
            body = Pair.Player.AttachedEntity!.Value;
            Assert.That(sEntMan.HasComponent<MobStateComponent>(body), "the test player isn't crew");
        });

        await client.WaitPost(() => cNet.SendSystemNetworkMessage(new FSReadyUpRequestMessage(true)));
        await Pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            Assert.That(overlays.TryGetOverlay<WaveHudOverlay>(out var overlay), "no wave HUD");
            Assert.That(overlay!.ReadyUpPlayerIsReady, "the ready press didn't reach the HUD");

            // What a reconnected client starts with.
            overlay.ReadyUpPlayerIsReady = false;
            overlay.ReadyUpCount = 0;
            overlay.ReadyUpTotal = 0;
        });

        await server.WaitPost(() =>
        {
            players.SetAttachedEntity(Pair.Player!, null);
            players.SetAttachedEntity(Pair.Player!, body);
        });
        await Pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            Assert.That(overlays.TryGetOverlay<WaveHudOverlay>(out var overlay));
            Assert.Multiple(() =>
            {
                Assert.That(overlay!.ReadyUpPlayerIsReady, "the HUD wasn't told the player is still ready");
                Assert.That(overlay.ReadyUpTotal, Is.EqualTo(1));
                Assert.That(overlay.ReadyUpCount, Is.EqualTo(1));
            });
        });
    }
}
