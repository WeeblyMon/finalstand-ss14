using System.Collections.Generic;
using System.Linq;
using Content.Client._FinalStand.Leaderboard;
using Content.Client._FinalStand.Profile;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Mentor;
using Content.Server._FinalStand.Titles;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Leaderboard;
using Content.Shared._FinalStand.Titles;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ProfileUiTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private async Task Send(EntityEventArgs message)
    {
        await Task.Delay(TimeSpan.FromSeconds(0.3));
        await Client.WaitPost(() => CEntMan.EntityNetManager.SendSystemNetworkMessage(message));
        await Pair.RunTicksSync(15);
    }

    [Test]
    public async Task TitlesCanBeSeenAndEquippedFromTheLobbyWindow()
    {
        var user = ServerSession!.UserId;
        await Server.WaitPost(() =>
        {
            SEntMan.System<FSPlayerDataStore>().DeleteProgress(user.UserId);
            var titles = SEntMan.System<FSTitleSystem>();
            titles.Grant(user, "FSTitleMentor");
            titles.Grant(user, "FSTitleFreshMeat");
            titles.Equip(user, null);
        });

        var profile = Client.ResolveDependency<IUserInterfaceManager>().GetUIController<FSProfileUIController>();
        await Send(new FSTitlesRequestEvent());
        await Client.WaitAssertion(() => Assert.That(profile.EquippedTitle, Is.Null));

        await Send(new FSTitleEquipEvent { Title = "FSTitleMentor" });
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<FSTitleSystem>().GetEquipped(user)?.Id, Is.EqualTo("FSTitleMentor")));
        await Client.WaitAssertion(() => Assert.That(profile.EquippedTitle, Is.EqualTo("FSTitleMentor"),
            "the lobby hears about the change straight away"));

        await Send(new FSTitleEquipEvent { Title = "FSTitleGuardianAngel" });
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<FSTitleSystem>().GetEquipped(user)?.Id, Is.EqualTo("FSTitleMentor"),
                "a title you don't own can't be equipped by sending its id"));

        await Send(new FSTitleEquipEvent { Title = null });
        await Server.WaitAssertion(() => Assert.That(SEntMan.System<FSTitleSystem>().GetEquipped(user), Is.Null));
    }

    [Test]
    public async Task TheMentorShopSellsTitlesForPoints()
    {
        var user = ServerSession!.UserId;
        await Server.WaitPost(() =>
        {
            var store = SEntMan.System<FSPlayerDataStore>();
            store.DeleteProgress(user.UserId);
            store.SaveMentor(user.UserId, new FSPlayerDataStore.MentorRecord(80, 80, FSWeeklyRules.WeekKey(DateTime.UtcNow), 0));
        });

        FSMentorStateEvent state = null;
        var relay = CEntMan.System<Content.Client._FinalStand.Interface.FSUiEventRelaySystem>();
        await Client.WaitPost(() => relay.MentorUpdated += ev => state = ev);

        await Send(new FSMentorRequestEvent());
        await Client.WaitAssertion(() =>
        {
            Assert.That(state, Is.Not.Null);
            Assert.That(state.Balance, Is.EqualTo(80));
            Assert.That(state.Shop.Select(s => s.Id), Is.EquivalentTo(FSMentorSystem.Shop.Select(s => s.Id)));
            Assert.That(state.Shop.Any(s => s.Owned), Is.False);
        });

        await Send(new FSMentorBuyEvent { Item = "guardian" });
        await Client.WaitAssertion(() =>
        {
            Assert.That(state.Balance, Is.EqualTo(80), "can't afford it");
            Assert.That(state.Message, Is.Not.Null);
        });

        await Send(new FSMentorBuyEvent { Item = "mentor" });
        await Client.WaitAssertion(() =>
        {
            Assert.That(state.Balance, Is.EqualTo(30));
            Assert.That(state.Shop.Single(s => s.Id == "mentor").Owned, Is.True);
        });
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<FSTitleSystem>().GetOwned(user).Select(t => t.Id), Does.Contain("FSTitleMentor")));
    }

    [Test]
    public async Task TheLeaderboardFillsTheTableAndPodium()
    {
        await Client.WaitAssertion(() =>
        {
            var window = new FSLeaderboardWindow();
            window.Populate(FSLeaderboardEntry.Sort(new[]
            {
                new FSLeaderboardEntry("Ada", 40, 3, 120, 0, 9, 0, 900, 4000),
                new FSLeaderboardEntry("Bo", 10, 1, 0, 0, 3, 0, 100, 900, Rookie: true),
            }));

            var labels = Descendants(window).OfType<Label>().Select(l => l.Text ?? string.Empty).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(labels.Count(t => t == "Ada"), Is.EqualTo(2), "first place is on the podium and in the table");
                Assert.That(labels, Does.Contain("Bo"));
                Assert.That(labels, Does.Contain("4,000"));
            });
            window.Dispose();
        });
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Children)
        {
            yield return child;
            foreach (var deeper in Descendants(child))
                yield return deeper;
        }
    }
}
