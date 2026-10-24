using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.GameTicking;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Tutorial;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Grenades;
using Content.Shared._FinalStand.Tutorial;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class TutorialTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task EachTipShowsOncePerPlayerAndSurvivesRestarts()
    {
        await Server.WaitAssertion(() =>
        {
            var tutorial = SEntMan.System<FSNewPlayerTipSystem>();
            var store = SEntMan.System<FSPlayerDataStore>();
            var session = ServerSession!;
            store.DeleteProgress(session.UserId.UserId);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            Assert.That(session.Status, Is.EqualTo(SessionStatus.InGame), "test precondition");
            Assert.That(tutorial.WantsTips(session), Is.True, "a fresh rookie gets tips");

            Assert.That(tutorial.TryShow(session, FSTutorialTip.Welcome), Is.True);
            Assert.That(tutorial.TryShow(session, FSTutorialTip.Welcome), Is.False, "each tip shows once");
            Assert.That(tutorial.TryShow(session, FSTutorialTip.Downed), Is.True, "other tips are separate");

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Assert.Multiple(() =>
            {
                Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.Welcome), Is.True, "seen tips are saved");
                Assert.That(tutorial.TryShow(session, FSTutorialTip.Welcome), Is.False);
                Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.BossWave), Is.False);
            });
        });
    }

    [Test]
    public async Task PlayersCanTurnTipsOffAndReplayThem()
    {
        await Server.WaitAssertion(() =>
        {
            var tutorial = SEntMan.System<FSNewPlayerTipSystem>();
            var session = ServerSession!;
            SEntMan.System<FSPlayerDataStore>().DeleteProgress(session.UserId.UserId);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            tutorial.SetEnabled(session.UserId, false);
            Assert.That(tutorial.WantsTips(session), Is.False);
            Assert.That(tutorial.TryShow(session, FSTutorialTip.Prep), Is.False, "opted out");

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Assert.That(tutorial.WantsTips(session), Is.False, "opting out is saved");

            tutorial.TryShow(session, FSTutorialTip.Welcome);
            tutorial.Replay(session.UserId);
            Assert.Multiple(() =>
            {
                Assert.That(tutorial.WantsTips(session), Is.True, "replay turns tips back on");
                Assert.That(tutorial.TryShow(session, FSTutorialTip.Prep), Is.True);
            });
        });
    }
}

[TestFixture]
public sealed class EnemyTipTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    [Test]
    public async Task EverySpecialZombieHasATipWithText()
    {
        await Server.WaitAssertion(() =>
        {
            var loc = Server.ResolveDependency<Robust.Shared.Localization.ILocalizationManager>();
            var rule = SProtoMan.Index(FSTestRules.WaveSurvival);
            Assert.That(rule.TryComp<WaveGameRuleComponent>(out var waves, SEntMan.ComponentFactory), Is.True);
            var specials = waves!.SpecialEnemyPool.Select(s => s.EnemyId.Id)
                .Concat(waves.EnemyConfigs.SelectMany(c => c.EnemyPool).Select(p => p.Id))
                .Where(id => id != "FSZombieNormal")
                .Distinct()
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(specials, Is.Not.Empty, "test precondition");
                foreach (var id in specials)
                    Assert.That(FSTutorialText.ForEnemy(id), Is.Not.Null, $"{id} spawns in waves but has no tip");

                foreach (var tip in Enum.GetValues<FSTutorialTip>())
                {
                    Assert.That(loc.HasString(FSTutorialText.TitleLocId(tip)), Is.True, $"{tip} has no title");
                    Assert.That(loc.HasString(FSTutorialText.BodyLocId(tip)), Is.True, $"{tip} has no body");
                    if (FSTutorialText.Icon(tip) is { } icon)
                        Assert.That(SProtoMan.HasIndex(icon), Is.True, $"{tip} shows {icon}, which doesn't exist");
                }

                var keys = Enum.GetValues<FSTutorialTip>().Select(FSTutorialText.Key).ToList();
                Assert.That(keys, Is.Unique, "every tip saves under its own key");
            });
        });
    }

    [Test]
    public async Task TheFirstBloaterOfTheRoundExplainsItself()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var tutorial = SEntMan.System<FSNewPlayerTipSystem>();
            var spawning = SEntMan.System<WaveEnemySpawningSystem>();
            var session = ServerSession!;
            SEntMan.System<FSPlayerDataStore>().DeleteProgress(session.UserId.UserId);

            Assert.That(session.AttachedEntity, Is.Not.Null, "test precondition");
            Assert.That(tutorial.WantsTips(session), Is.True, "test precondition");
            Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival, out var ruleUid), Is.True);
            var rule = SEntMan.GetComponent<WaveGameRuleComponent>(ruleUid);
            Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.Bloater), Is.False, "test precondition");

            spawning.SpawnWaveEnemy("FSZombieBloater", map.GridCoords, rule);
            Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.Bloater), Is.True);
            Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.Tesla), Is.False, "only the zombie that showed up is explained");

            spawning.SpawnWaveEnemy("FSZombieNormal", map.GridCoords, rule);
            Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.Runner), Is.False, "plain zombies need no tip");
        });
    }
}

[TestFixture]
public sealed class GameplayTipTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    [Test]
    public async Task TheConsoleAndGrenadeTipsShowWhenTheyMatter()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var tutorial = SEntMan.System<FSNewPlayerTipSystem>();
            var session = ServerSession!;
            tutorial.Replay(session.UserId);
            Assert.That(session.AttachedEntity, Is.Not.Null, "test precondition");
            Assert.That(tutorial.WantsTips(session), Is.True, "test precondition");

            var ccc = SEntMan.SpawnEntity("FinalStandCCC", map.GridCoords);
            Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.CccAttacked), Is.False, "placing the console says nothing");
            var damage = new DamageSpecifier(SProtoMan.Index(Blunt), 10);
            SEntMan.System<DamageableSystem>().TryChangeDamage(ccc, damage, ignoreResistances: true);
            Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.CccAttacked), Is.True);

            Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.Grenades), Is.False, "test precondition");
            SEntMan.EnsureComponent<FSActiveGrenadeComponent>(session.AttachedEntity!.Value);
            Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.Grenades), Is.True,
                "picking up a grenade pack explains the throw key");
        });
    }

    [Test]
    public async Task TheConsoleTipIsCheckedAtMostOnceASecond()
    {
        var map = await Pair.CreateTestMap();
        var ccc = EntityUid.Invalid;

        void HitConsole()
        {
            var damage = new DamageSpecifier(SProtoMan.Index(Blunt), 10);
            SEntMan.System<DamageableSystem>().TryChangeDamage(ccc, damage, ignoreResistances: true);
        }

        await Server.WaitAssertion(() =>
        {
            var tutorial = SEntMan.System<FSNewPlayerTipSystem>();
            var session = ServerSession!;
            ccc = SEntMan.SpawnEntity("FinalStandCCC", map.GridCoords);

            tutorial.Replay(session.UserId);
            HitConsole();
            Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.CccAttacked), Is.True, "test precondition");

            tutorial.Replay(session.UserId);
            HitConsole();
            Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.CccAttacked), Is.False,
                "every swing at the console doesn't re-check every player");
        });

        await RunSeconds(1.1f);

        await Server.WaitAssertion(() =>
        {
            HitConsole();
            Assert.That(SEntMan.System<FSNewPlayerTipSystem>().HasSeen(ServerSession!.UserId, FSTutorialTip.CccAttacked), Is.True,
                "someone who wanted tips after the first hit still gets one a second later");
        });
    }

    [Test]
    public async Task SpawningExplainsTheGoalAndTheControls()
    {
        await Server.WaitAssertion(() =>
        {
            var tutorial = SEntMan.System<FSNewPlayerTipSystem>();
            var session = ServerSession!;
            tutorial.Replay(session.UserId);
            Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival), Is.True);
            Assert.That(SEntMan.System<WaveGameRuleSystem>().TryGetWaveStatus(out _, out var phase), Is.True);
            Assert.That(phase, Is.EqualTo(WavePhase.Prep), "test precondition");

            tutorial.ShowSpawnTips(session);

            Assert.Multiple(() =>
            {
                Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.Welcome), Is.True);
                Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.Controls), Is.True);
                Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.Prep), Is.True, "the round starts in prep");
                Assert.That(tutorial.HasSeen(session.UserId, FSTutorialTip.PerkShop), Is.True);
            });
        });
    }
}

[TestFixture]
public sealed class TutorialCardTest : GameTest
{
    [Test]
    public async Task TipsQueueAndDismissInOrder()
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<Robust.Client.UserInterface.IUserInterfaceManager>()
                .GetUIController<Content.Client._FinalStand.Tutorial.FSTutorialUIController>();
            ui.Reset();

            ui.Enqueue(FSTutorialTip.Welcome);
            ui.Enqueue(FSTutorialTip.Prep);
            ui.Enqueue(FSTutorialTip.Prep);
            Assert.That(ui.Showing, Is.EqualTo(FSTutorialTip.Welcome));

            ui.Dismiss();
            Assert.That(ui.Showing, Is.EqualTo(FSTutorialTip.Prep), "queued tips follow in order");

            ui.Dismiss();
            Assert.That(ui.Showing, Is.Null, "a duplicate tip is not queued twice");
        });
    }

    [Test]
    public async Task CardsShowTheThingTheyTalkAboutAndTheRealKeys()
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<Robust.Client.UserInterface.IUserInterfaceManager>()
                .GetUIController<Content.Client._FinalStand.Tutorial.FSTutorialUIController>();
            ui.Reset();

            ui.Enqueue(FSTutorialTip.PerkShop);
            Assert.That(ui.ShowingIcon?.Id, Is.EqualTo("FSPerkShop"));
            ui.Dismiss();

            ui.Enqueue(FSTutorialTip.Controls);
            Assert.Multiple(() =>
            {
                Assert.That(ui.ShowingIcon, Is.Null, "the controls card has no picture");
                Assert.That(ui.ShowingBody, Does.Not.Contain("{$"), "every key name is filled in");
                Assert.That(ui.ShowingBody, Does.Contain("[bold]R[/bold]"), "reload shows the bound key");
                Assert.That(ui.ShowingBody, Does.Contain("[bold]G[/bold]"));
                Assert.That(ui.ShowingBody, Does.Contain("[bold]F[/bold]"));
            });
            ui.Reset();
        });
    }

    [Test]
    public async Task TheReadyUpCardPointsAtTheWaveHudButton()
    {
        await Client.WaitAssertion(() =>
        {
            var overlays = Client.ResolveDependency<Robust.Client.Graphics.IOverlayManager>();
            if (!overlays.TryGetOverlay<Content.Client._FinalStand.WaveHud.WaveHudOverlay>(out var hud))
            {
                hud = new Content.Client._FinalStand.WaveHud.WaveHudOverlay();
                overlays.AddOverlay(hud);
            }
            hud.IsReadyUpVisible = true;
            hud.ReadyUpPlayerIsReady = false;
            hud.ReadyUpYesBounds = new Robust.Shared.Maths.UIBox2(1200, 300, 1260, 320);

            var ui = Client.ResolveDependency<Robust.Client.UserInterface.IUserInterfaceManager>()
                .GetUIController<Content.Client._FinalStand.Tutorial.FSTutorialUIController>();
            ui.Reset();
            ui.Enqueue(FSTutorialTip.ReadyUp);
            ui.FrameUpdate(new Robust.Shared.Timing.FrameEventArgs(0.016f));
            Assert.That(ui.ShowingArrow, Is.True, "an arrow points at the YES button");

            hud.ReadyUpPlayerIsReady = true;
            ui.FrameUpdate(new Robust.Shared.Timing.FrameEventArgs(0.016f));
            Assert.Multiple(() =>
            {
                Assert.That(ui.Showing, Is.Null, "the card goes away once you ready up");
                Assert.That(ui.ShowingArrow, Is.False);
            });
            hud.IsReadyUpVisible = false;
            hud.ReadyUpPlayerIsReady = false;
        });
    }
}

[TestFixture]
public sealed class TutorialCardDisconnectTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task ADisconnectDropsTheQueuedCards()
    {
        var name = ServerSession!.Name;
        var ui = Client.ResolveDependency<Robust.Client.UserInterface.IUserInterfaceManager>()
            .GetUIController<Content.Client._FinalStand.Tutorial.FSTutorialUIController>();
        await Client.WaitPost(() =>
        {
            ui.Reset();
            ui.Enqueue(FSTutorialTip.Welcome);
            ui.Enqueue(FSTutorialTip.PerkShop);
        });

        var net = Client.ResolveDependency<Robust.Shared.Network.IClientNetManager>();
        await Client.WaitPost(() => net.ClientDisconnect("testing a disconnect"));
        await Pair.RunTicksSync(10);

        // The next card looks up its picture through SpriteSystem, which went down with the connection.
        await Client.WaitAssertion(() =>
        {
            Assert.That(ui.Showing, Is.Null, "the cards go with the server that sent them");
            Assert.DoesNotThrow(ui.Dismiss);
        });

        Client.SetConnectTarget(Server);
        await Client.WaitPost(() => net.ClientConnect(null!, 0, name));
        await Pair.RunTicksSync(30);
    }
}
