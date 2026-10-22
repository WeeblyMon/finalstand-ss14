using System;
using System.Numerics;
using Content.Client._FinalStand.Pings;
using Content.Client._FinalStand.UI;
using Content.Client._FinalStand.WaveHud;
using Content.IntegrationTests.Fixtures;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.FinalStand;

// The HUD is drawn under the UI and reads the mouse button itself. A build ring, the ping wheel or a popup closes on
// the click that picks from it, before the HUD looks, so that click used to land on whatever HUD button was under it
// too: picking a ping over the ready-up prompt readied the player.
[TestFixture]
[TestOf(typeof(WaveHudOverlay))]
public sealed class HudClickThroughTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true };

    [TestCase("ring")]
    [TestCase("wheel")]
    [TestCase("popup")]
    public async Task ClickThatClosesAPickerMissesTheHud(string picker)
    {
        var client = Pair.Client;
        var overlays = client.ResolveDependency<IOverlayManager>();
        var ui = client.ResolveDependency<IUserInterfaceManager>();
        var cache = client.ResolveDependency<IResourceCache>();

        var clicks = 0;
        WaveHudOverlay hud = null;
        WaveHudOverlay realHud = null;
        await client.WaitAssertion(() =>
        {
            // One overlay per type: the HUD the client already has, if an earlier test made one, steps aside.
            if (overlays.TryGetOverlay(out realHud))
                overlays.RemoveOverlay(realHud);

            // A ready-up button under the whole screen, wherever the headless client's mouse is.
            hud = new WaveHudOverlay
            {
                IsReadyUpVisible = true,
                ReadyUpYesBounds = new UIBox2(-100000f, -100000f, 100000f, 100000f),
            };
            hud.OnReadyUpClicked += _ => clicks++;
            Assert.That(overlays.AddOverlay(hud), "couldn't put the test HUD up");
        });

        try
        {
            await Click();
            Assert.That(clicks, Is.EqualTo(1), "a plain click on the HUD button didn't count");

            Control open = null;
            await client.WaitPost(() =>
            {
                switch (picker)
                {
                    case "ring":
                        open = new TestRing();
                        ui.PopupRoot.AddChild(open);
                        break;
                    case "wheel":
                        open = new FSPingWheel(new FSPingLook(cache), Vector2.Zero, clickToPick: true);
                        ui.PopupRoot.AddChild(open);
                        break;
                    default:
                        var popup = new Popup();
                        ui.ModalRoot.AddChild(popup);
                        popup.Open();
                        open = popup;
                        break;
                }
            });
            await client.WaitRunTicks(1);

            // The click picks from it and shuts it during input handling, before the HUD's frame update.
            await Click(before: () =>
            {
                if (open is Popup popup)
                    popup.Close();
                open.Orphan();
            });
            Assert.That(clicks, Is.EqualTo(1), $"the click that closed the {picker} also hit the HUD button under it");

            await Click();
            Assert.That(clicks, Is.EqualTo(2), $"the HUD stopped taking clicks after the {picker} closed");
        }
        finally
        {
            await client.WaitPost(() =>
            {
                overlays.RemoveOverlay(hud);
                if (realHud != null)
                    overlays.AddOverlay(realHud);
            });
        }
    }

    private async Task Click(Action before = null)
    {
        var client = Pair.Client;
        var input = client.ResolveDependency<IInputManager>();
        await client.WaitPost(() =>
        {
            before?.Invoke();
            input.KeyDown(new KeyEventArgs(Keyboard.Key.MouseLeft, false, false, false, false, false, 0));
        });
        await client.WaitRunTicks(1);
        await client.WaitPost(() => input.KeyUp(new KeyEventArgs(Keyboard.Key.MouseLeft, false, false, false, false, false, 0)));
        await client.WaitRunTicks(2);
    }

    private sealed class TestRing : FSRingControl
    {
        public TestRing() : base(4, Vector2.Zero)
        {
        }

        protected override int SlotCount => 4;

        protected override void DrawSlot(DrawingHandleScreen handle, int index, Vector2 pos, bool hot, float scale)
        {
        }

        protected override void DrawCenter(DrawingHandleScreen handle, Vector2 center, float disc, float scale)
        {
        }

        protected override void Clicked(int index)
        {
        }
    }
}
