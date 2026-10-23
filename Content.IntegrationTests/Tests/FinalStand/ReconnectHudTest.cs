using System.Collections.Generic;
using System.Linq;
using Content.Client._FinalStand.Ladder;
using Content.Client._FinalStand.Tutorial;
using Content.Client._FinalStand.WaveHud;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using Content.Shared._FinalStand.Tutorial;
using Content.Shared.Mind;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ReconnectHudTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Children)
        {
            yield return child;
            foreach (var deeper in Descendants(child))
                yield return deeper;
        }
    }

    private List<string> XpBars()
    {
        var root = Client.ResolveDependency<IUserInterfaceManager>().RootControl;
        return Descendants(root).OfType<Label>().Select(l => l.Text ?? "").Where(t => t.StartsWith("LVL") || t.Contains("LVL ")).ToList();
    }

    private string XpBarText() => XpBars().FirstOrDefault() ?? string.Empty;

    private async Task Disconnect()
    {
        await Client.WaitPost(() => Client.ResolveDependency<IClientNetManager>().ClientDisconnect("testing a reconnect"));
        await Pair.RunTicksSync(10);
        Assert.That(Server.ResolveDependency<IPlayerManager>().Sessions, Is.Empty);
    }

    private async Task Connect(string name)
    {
        Client.SetConnectTarget(Server);
        await Client.WaitPost(() => Client.ResolveDependency<IClientNetManager>().ClientConnect(null!, 0, name));
        await Pair.RunTicksSync(30);
        Assert.That(Server.ResolveDependency<IPlayerManager>().Sessions.Single().Status, Is.EqualTo(SessionStatus.InGame));
    }

    [Test]
    public async Task TheHudCatchesUpAfterAReconnect()
    {
        var name = ServerSession!.Name;
        var mindId = EntityUid.Invalid;
        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival), Is.True);
            Assert.That(SEntMan.System<SharedMindSystem>().TryGetMind(ServerSession!, out mindId, out _), Is.True);
        });
        await Pair.RunTicksSync(10);

        await Disconnect();

        var serverWave = 0;
        var wavesHeld = 0;
        var serverPhase = WavePhase.Combat;
        const int serverLevel = 7;
        await Server.WaitAssertion(() =>
        {
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            waveRule.ForceNextWave(3);
            waveRule.ForceNextWave();
            Assert.That(waveRule.TryGetWaveStatus(out serverWave, out serverPhase), Is.True);
            Assert.That(serverPhase, Is.EqualTo(WavePhase.Prep), "test precondition: between waves");
            var rule = SEntMan.EntityQuery<WaveGameRuleComponent>().Single();
            wavesHeld = (int) typeof(WaveGameRuleComponent).GetField(nameof(WaveGameRuleComponent.WavesCompleted))!.GetValue(rule)!;
            Assert.That(wavesHeld, Is.GreaterThan(0), "test precondition");

            var lvl = SEntMan.GetComponent<FSPlayerLevelComponent>(mindId);
            lvl.Level = serverLevel;
            lvl.XpToNextLevel = FSLevelingSystem.XpToNextLevel(serverLevel);
        });

        await Connect(name);

        await Client.WaitAssertion(() =>
        {
            Assert.That(Client.ResolveDependency<IOverlayManager>().TryGetOverlay<WaveHudOverlay>(out var overlay), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(overlay!.CurrentWave, Is.EqualTo(wavesHeld), "the wave counter catches up with what happened while away");
                Assert.That(overlay.IsPrepPhase, Is.True);
                Assert.That(overlay.IsReadyUpVisible, Is.True, "the ready-up button is back without waiting for the next wave");
                Assert.That(XpBars(), Has.Count.EqualTo(1), "one XP bar, not a stale copy from before the reconnect");
                Assert.That(XpBarText(), Does.Contain($"LVL {serverLevel}"), "the XP bar shows the real level");
            });
        });

        var before = XpBarText();
        await Server.WaitPost(() =>
        {
            var ended = new WaveEndedEvent(serverWave);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ended);
        });
        await Pair.RunTicksSync(10);

        await Client.WaitAssertion(() =>
        {
            Assert.That(XpBarText(), Is.Not.EqualTo(before), "and it keeps updating as XP comes in");
        });
    }

    [Test]
    public async Task TheUiStillListensAfterAReconnect()
    {
        var name = ServerSession!.Name;
        await Disconnect();
        await Connect(name);

        var ui = Client.ResolveDependency<IUserInterfaceManager>();
        await Client.WaitPost(() => ui.GetUIController<FSTutorialUIController>().Reset());

        await Server.WaitPost(() =>
        {
            var channel = ServerSession!.Channel;
            SEntMan.EntityNetManager.SendSystemNetworkMessage(new FSTutorialTipEvent { Tip = FSTutorialTip.Bloater }, channel);
            SEntMan.EntityNetManager.SendSystemNetworkMessage(new FSOpenLadderEvent(), channel);
        });
        await Pair.RunTicksSync(15);

        await Client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(ui.GetUIController<FSTutorialUIController>().Showing, Is.EqualTo(FSTutorialTip.Bloater),
                    "tutorial cards still arrive after a reconnect");
                Assert.That(ui.WindowRoot.Children.OfType<FSLadderWindow>().Any(), Is.True,
                    "the record board can still open the ladder window");
            });

            foreach (var window in ui.WindowRoot.Children.OfType<FSLadderWindow>().ToList())
                window.Close();
            ui.GetUIController<FSTutorialUIController>().Reset();
        });
    }
}
