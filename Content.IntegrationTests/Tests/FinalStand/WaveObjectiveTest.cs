using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Objectives;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class WaveObjectiveTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    private (EntityUid Mind, EntityUid Body) SetUp(Robust.Shared.Map.EntityCoordinates coords)
    {
        var minds = SEntMan.System<SharedMindSystem>();
        var session = ServerSession!;
        if (!minds.TryGetMind(session, out var mindId, out var mind) || mind.OwnedEntity is not { } body)
        {
            body = SEntMan.SpawnEntity("MobHuman", coords);
            mindId = minds.CreateMind(session.UserId, "Defender");
            minds.TransferTo(mindId, body, mind: SEntMan.GetComponent<MindComponent>(mindId));
        }

        SEntMan.EnsureComponent<FSPlayerWalletComponent>(mindId);
        Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival), Is.True);
        SEntMan.System<WaveGameRuleSystem>().ForceNextWave(6);
        return (mindId, body);
    }

    private FSWaveObjectivePrototype Objective(string id) => SProtoMan.Index<FSWaveObjectivePrototype>(id);

    [Test]
    public async Task HittingTheKillQuotaPaysEveryone()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var objectives = SEntMan.System<FSWaveObjectiveSystem>();
            var (mindId, body) = SetUp(map.GridCoords);
            var wallet = SEntMan.GetComponent<FSPlayerWalletComponent>(mindId);

            Assert.That(objectives.TryStart(Objective("FSObjectiveCullTheHerd")), Is.True);
            var current = objectives.Current!;
            Assert.That(current.Target, Is.EqualTo(25), "a lone defender gets the base quota");

            for (var i = 0; i < current.Target - 1; i++)
                Kill(body);
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.InProgress));

            var orphan = new FSWaveEnemyDiedEvent(EntityUid.Invalid, null);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref orphan);
            Assert.That(current.Progress, Is.EqualTo(current.Target - 1), "kills nobody made don't count");

            // Each kill also pays its own credits, so the objective is what the completing kill paid over a plain one.
            var before = wallet.Credits;
            Kill(body);
            var completing = wallet.Credits - before;
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.Completed));

            before = wallet.Credits;
            Kill(body);
            var plainKill = wallet.Credits - before;
            Assert.That(completing - plainKill, Is.EqualTo(300 + 30 * 6), "it pays once, on the kill that completes it");
        });
    }

    private void Kill(EntityUid killer)
    {
        var died = new FSWaveEnemyDiedEvent(EntityUid.Invalid, killer);
        SEntMan.EventBus.RaiseEvent(EventSource.Local, ref died);
    }

    [Test]
    public async Task RunningOutOfTimeFailsTheObjective()
    {
        var map = await Pair.CreateTestMap();
        FSWaveObjectiveSystem.ActiveObjective current = null;

        await Server.WaitAssertion(() =>
        {
            var objectives = SEntMan.System<FSWaveObjectiveSystem>();
            SetUp(map.GridCoords);
            Assert.That(objectives.TryStart(Objective("FSObjectiveCullTheHerd")), Is.True);
            current = objectives.Current!;
            current.EndTime = Server.ResolveDependency<Robust.Shared.Timing.IGameTiming>().CurTime;
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.Failed));
        });
    }

    [Test]
    public async Task GoingDownFailsNobodyFalls()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var objectives = SEntMan.System<FSWaveObjectiveSystem>();
            var (_, body) = SetUp(map.GridCoords);
            Assert.That(SEntMan.HasComponent<ActorComponent>(body), Is.True, "test precondition: the body is a player's");

            Assert.That(objectives.TryStart(Objective("FSObjectiveNobodyFalls")), Is.True);
            SEntMan.System<MobStateSystem>().ChangeMobState(body, MobState.Critical);
            Assert.That(objectives.Current!.Result, Is.EqualTo(FSWaveObjectiveResult.Failed));
        });
    }

    [Test]
    public async Task ClearingTheWaveCompletesNobodyFallsAndPrepClearsTheHud()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var objectives = SEntMan.System<FSWaveObjectiveSystem>();
            SetUp(map.GridCoords);

            Assert.That(objectives.TryStart(Objective("FSObjectiveNobodyFalls")), Is.True);
            var current = objectives.Current!;
            var ended = new WaveEndedEvent(6);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ended);
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.Completed));

            SEntMan.System<WaveGameRuleSystem>().ForceNextWave();
            Assert.That(objectives.Current, Is.Null, "the next prep phase clears the objective");
        });
    }

    [Test]
    public async Task BossWavesAndPrepNeverRollObjectives()
    {
        await Server.WaitAssertion(() =>
        {
            var objectives = SEntMan.System<FSWaveObjectiveSystem>();
            Assert.That(objectives.TryStartRandom(), Is.False, "no round, no objective");
            Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival), Is.True);
            Assert.That(objectives.TryStartRandom(), Is.False, "nothing during prep");

            SEntMan.System<WaveGameRuleSystem>().ForceNextWave(10);
            Assert.That(objectives.TryStartRandom(), Is.False, "boss waves are left alone");
            Assert.That(objectives.Current, Is.Null);
        });
    }
}
