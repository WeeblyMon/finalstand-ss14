using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.CCC;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Effects;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.MedicalOps;
using Content.Server._FinalStand.Objectives;
using Content.Server._FinalStand.Perks;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.CCC;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Objectives;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class WaveObjectiveKindsTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    private FSWaveObjectiveSystem Objectives => SEntMan.System<FSWaveObjectiveSystem>();

    private (EntityUid Mind, EntityUid Body) StartCombat(EntityCoordinates coords)
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
        Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival, out var rule), Is.True);
        SEntMan.System<WaveGameRuleSystem>().ForceNextWave(6);

        _rule = rule;
        _spawner = SEntMan.SpawnEntity(null, coords.Offset(new Vector2(20, 0)));
        return (mindId, body);
    }

    private EntityUid _rule;
    private EntityUid _spawner;

    private T WithSpawner<T>(Func<T> action)
    {
#pragma warning disable RA0002
        var spawners = SEntMan.GetComponent<WaveGameRuleComponent>(_rule).SpawnerEntities;
        var saved = spawners.ToList();
        spawners.Clear();
        spawners.Add(_spawner);
        try
        {
            return action();
        }
        finally
        {
            spawners.Clear();
            spawners.AddRange(saved);
        }
#pragma warning restore RA0002
    }

    private FSWaveObjectiveSystem.ActiveObjective Start(string id)
    {
        Assert.That(WithSpawner(() => Objectives.TryStart(SProtoMan.Index<FSWaveObjectivePrototype>(id))), Is.True);
        return Objectives.Current!;
    }

    private void Died(EntityUid enemy, EntityUid? killer)
    {
        var died = new FSWaveEnemyDiedEvent(enemy, killer);
        SEntMan.EventBus.RaiseEvent(EventSource.Local, ref died);
    }

    private void EndWave()
    {
        var ended = new WaveEndedEvent(6);
        SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ended);
    }

    [Test]
    public async Task HoldTheLineFailsOnceTheConsoleLosesTooMuch()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            StartCombat(map.GridCoords);
            var ccc = SEntMan.SpawnEntity("FinalStandCCC", map.GridCoords);
            var max = SEntMan.GetComponent<FinalStandCCCComponent>(ccc).MaxHealth;
            Assert.That(max, Is.GreaterThan(0f), "test precondition");

            var current = Start("FSObjectiveHoldTheLine");
            var damageable = SEntMan.System<DamageableSystem>();
            damageable.TryChangeDamage(ccc, new DamageSpecifier(SProtoMan.Index(Blunt), max * 0.05f), ignoreResistances: true);
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.InProgress));
            Assert.That(current.Progress, Is.EqualTo(5));

            damageable.TryChangeDamage(ccc, new DamageSpecifier(SProtoMan.Index(Blunt), max * 0.06f), ignoreResistances: true);
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.Failed));
        });
    }

    [Test]
    public async Task HeadhunterNeedsTheMarkedZombie()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var (_, body) = StartCombat(map.GridCoords);
            var current = Start("FSObjectiveHeadhunter");
            if (current.Marked.Count == 0)
                WithSpawner(() => SEntMan.System<WaveGameRuleSystem>().SpawnExtraEnemy("FSZombieNormal"));

            var target = current.Marked.Single();
            Assert.That(SEntMan.GetComponent<FSObjectiveMarkerComponent>(target).Label.Id, Is.EqualTo("fs-objective-marker-target"));

            Died(EntityUid.Invalid, body);
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.InProgress), "any other kill doesn't count");

            Died(target, body);
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.Completed));
            Assert.That(SEntMan.HasComponent<FSObjectiveMarkerComponent>(target), Is.False, "the marker comes off");
        });
    }

    [Test]
    public async Task FortressFailsWhenAStructureFalls()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            StartCombat(map.GridCoords);
            var current = Start("FSObjectiveFortress");
            var destroyed = new FSStructureDestroyedEvent(EntityUid.Invalid);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref destroyed);
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.Failed));

            current = Start("FSObjectiveFortress");
            EndWave();
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.Completed), "a wave with nothing lost pays out");
        });
    }

    [Test]
    public async Task MedicOnDutyCountsHealingOnTeammates()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var (mind, body) = StartCombat(map.GridCoords);
            var current = Start("FSObjectiveMedicOnDuty");
            Assert.That(current.Target, Is.EqualTo(200));

            var healed = new FSAllyHealedEvent(mind, body, 150f);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref healed);
            Assert.That(current.Progress, Is.EqualTo(150));
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref healed);
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.Completed));
        });
    }

    [Test]
    public async Task BountyRunnersPayWhenAllAreCaughtAndEscapeOtherwise()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var (_, body) = StartCombat(map.GridCoords);
            var current = Start("FSObjectiveBountySwarm");
            var bounties = current.Marked.ToList();
            Assert.That(bounties, Has.Count.EqualTo(3), "one runner per bounty, spawned at the zombie spawns");
            Assert.That(SEntMan.System<WaveGameRuleSystem>().AliveEnemies(), Is.SupersetOf(bounties),
                "they count as wave enemies");
            Assert.That(bounties.Select(b => SEntMan.GetComponent<FSObjectiveMarkerComponent>(b).Tint),
                Is.All.EqualTo(FSWaveObjectiveSystem.BountyTint), "bounties are painted gold");
        });
        await Pair.RunTicksSync(5);

        List<EntityUid> caught = null;
        await Server.WaitPost(() => caught = Objectives.Current!.Marked.ToList());
        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<Robust.Client.GameObjects.SpriteComponent>(CEntMan.GetEntity(SEntMan.GetNetEntity(caught[0])));
            Assert.That(sprite.Color, Is.EqualTo(FSWaveObjectiveSystem.BountyTint), "the client sees them gold");
        });

        await Server.WaitAssertion(() =>
        {
            var current = Objectives.Current!;
            var body = ServerSession!.AttachedEntity!.Value;
            foreach (var bounty in caught)
                Died(bounty, body);
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.Completed));

            current = Start("FSObjectiveBountySwarm");
            current.EndTime = TimeSpan.Zero;
        });
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Objectives.Current!.Result, Is.EqualTo(FSWaveObjectiveResult.Failed));
        });
    }

    [Test]
    public async Task SupplyRunCompletesWhenTheCrateReachesTheConsole()
    {
        var map = await Pair.CreateTestMap();
        EntityUid crate = default;
        await Server.WaitAssertion(() =>
        {
            StartCombat(map.GridCoords);
            SEntMan.SpawnEntity("FinalStandCCC", map.GridCoords);
            var current = Start("FSObjectiveSupplyRun");
            crate = current.Supply!.Value;
            Assert.That(current.Progress, Is.GreaterThan(FSWaveObjectiveSystem.SupplyDeliveryRange));
            SEntMan.System<SharedTransformSystem>().SetCoordinates(crate, map.GridCoords.Offset(new Vector2(1, 0)));
        });
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Objectives.Current!.Result, Is.EqualTo(FSWaveObjectiveResult.Completed));
            Assert.That(SEntMan.Deleted(crate) || SEntMan.IsQueuedForDeletion(crate), Is.True);
        });
    }

    [Test]
    public async Task LeanWaveFailsOnSpendingButNotOnTransfers()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var (mind, _) = StartCombat(map.GridCoords);
            var wallet = SEntMan.System<FSPlayerWalletSystem>();
            wallet.GiveCredits(mind, 1000);
            var current = Start("FSObjectiveLeanWave");

            Assert.That(wallet.TryDeductCredits(mind, 10, spending: false), Is.True);
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.InProgress), "sending a teammate cash isn't spending");

            Assert.That(wallet.TryDeductCredits(mind, 10), Is.True);
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.Failed));
        });
    }

    [Test]
    public async Task CloseQuartersOnlyCountsMeleeKills()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var (_, body) = StartCombat(map.GridCoords);
            var current = Start("FSObjectiveCloseQuarters");
            var hands = SEntMan.System<SharedHandsSystem>();

            var gun = SEntMan.SpawnEntity("WeaponPistolViper", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(body, gun), Is.True, "test precondition");
            Died(EntityUid.Invalid, body);
            Assert.That(current.Progress, Is.Zero, "a gun kill doesn't count");

            SEntMan.DeleteEntity(gun);
            Died(EntityUid.Invalid, body);
            Assert.That(current.Progress, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task UntouchableFailsWhenTooManyGetHurt()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var (_, body) = StartCombat(map.GridCoords);
            var current = Start("FSObjectiveUntouchable");
            Assert.That(current.Target, Is.EqualTo(1));

            var hurt = new FSPlayerHurtEvent(body);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref hurt);
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.Failed), "a lone defender has to stay untouched");

            current = Start("FSObjectiveUntouchable");
            EndWave();
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.Completed));
        });
    }

    [Test]
    public async Task ObjectiveRewardsCarryTheTitleBonus()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var (mind, _) = StartCombat(map.GridCoords);
            var titles = SEntMan.System<Content.Server._FinalStand.Titles.FSTitleSystem>();
            var user = ServerSession!.UserId;
            SEntMan.System<FSPlayerDataStore>().DeleteProgress(user.UserId);
            titles.Grant(user, "FSTitleWeeklyChampion");
            titles.Equip(user, "FSTitleWeeklyChampion");

            var wallet = SEntMan.GetComponent<FSPlayerWalletComponent>(mind);
            // The wave clear pays on the same event, without the title bonus.
            var rewards = SEntMan.EntityQuery<FSWaveRewardsComponent>().Single();
            var clearPay = rewards.ClearBase + rewards.ClearPerWave * 6;
            var before = wallet.Credits;
            var current = Start("FSObjectiveFortress");
            EndWave();
            Assert.That(current.Result, Is.EqualTo(FSWaveObjectiveResult.Completed));
            Assert.That(wallet.Credits - before, Is.EqualTo(clearPay + (int) MathF.Round((350 + 35 * 6) * 1.05f)));
        });
    }
}
