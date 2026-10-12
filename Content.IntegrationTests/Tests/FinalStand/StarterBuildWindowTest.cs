using System.Collections.Generic;
using Content.Client._FinalStand.Perks.UI;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Perks;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class StarterBuildWindowTest : GameTest
{
    [Test]
    public async Task TheStarterBannerOnlyShowsForAnEmptyBuild()
    {
        await Client.WaitAssertion(() =>
        {
            var window = new PerkShopWindow();
            var banner = window.FindControl<PanelContainer>("StarterBanner");
            var footerButton = window.FindControl<Button>("StarterBuildsButton");

            window.UpdateState(new FSPerksStateEvent { PerkPoints = 5 });
            Assert.That(banner.Visible, Is.True, "a player with no perks is offered a starter build up front");

            window.UpdateState(new FSPerksStateEvent
            {
                PerkPoints = 0,
                Levels = new Dictionary<string, int> { ["StoppingPower"] = 1 },
            });
            Assert.Multiple(() =>
            {
                Assert.That(banner.Visible, Is.False, "the banner gets out of the way once the player has a build");
                Assert.That(footerButton.Visible, Is.True, "starter builds stay reachable from the footer");
                Assert.That(footerButton.Text, Is.Not.Empty);
            });

            window.Dispose();
        });
    }
}
