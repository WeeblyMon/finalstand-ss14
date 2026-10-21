using System.Collections.Generic;
using Content.Client._FinalStand.WaveHud;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Perks;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// Perk stacks live on the mind, so a new round starts every perk at zero without the server sending one. The HUD has
// to drop last round's counts when the restart reaches it, which on the client only comes over the network.
[TestFixture]
[TestOf(typeof(WaveHudSystem))]
public sealed class PerkStackRestartTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true };

    [Test]
    public async Task RestartClearsPerkStacks()
    {
        var sNet = Pair.Server.ResolveDependency<IEntityManager>().EntityNetManager!;
        var overlays = Pair.Client.ResolveDependency<IOverlayManager>();

        await Pair.Server.WaitPost(() =>
            sNet.SendSystemNetworkMessage(new FSPerkStacksUpdateEvent { PerkId = "Thorns", Stacks = 4 }));
        await Pair.RunTicksSync(5);

        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(overlays.TryGetOverlay<WaveHudOverlay>(out var hud), "the HUD overlay never came up");
            Assert.That(hud.PerkStacks.GetValueOrDefault("Thorns"), Is.EqualTo(4), "the stack update never reached the HUD");
        });

        await Pair.Server.WaitPost(() => sNet.SendSystemNetworkMessage(new RoundRestartCleanupEvent()));
        await Pair.RunTicksSync(5);

        await Pair.Client.WaitAssertion(() =>
        {
            overlays.TryGetOverlay<WaveHudOverlay>(out var hud);
            Assert.That(hud.PerkStacks, Is.Empty, "last round's perk stacks survived the restart");
        });
    }
}
