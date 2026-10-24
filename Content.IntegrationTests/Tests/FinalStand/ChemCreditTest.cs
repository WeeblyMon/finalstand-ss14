using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.MedicalOps;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ChemCreditTest : GameTest
{
    private const string HumanProto = "MobHuman";
    private const string EnemyProto = "FSZombieNormal";

    [Test]
    public async Task ABuffedPlayersKillPaysTheChemist()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var credit = entMan.System<FSChemCreditSystem>();
            var wallet = entMan.System<FSPlayerWalletSystem>();
            var minds = entMan.System<SharedMindSystem>();

            var chemist = entMan.SpawnEntity(HumanProto, map.GridCoords);
            var soldier = entMan.SpawnEntity(HumanProto, map.GridCoords);
            entMan.EnsureComponent<FSFriendlyFireComponent>(soldier);

            var chemistMind = minds.CreateMind(null, "Chemist");
            minds.TransferTo(chemistMind, chemist, mind: entMan.GetComponent<MindComponent>(chemistMind));

            var before = wallet.GetCredits(chemistMind);

            credit.RegisterDelivery(soldier, chemist);
            KillEnemy(entMan, map.GridCoords, soldier);

            Assert.That(wallet.GetCredits(chemistMind), Is.GreaterThan(before),
                "buffing is unpaid work - the chemist would go back to brewing heal-chems");
        });
    }

    [Test]
    public async Task BuffingYourselfPaysNothing()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var credit = entMan.System<FSChemCreditSystem>();
            var wallet = entMan.System<FSPlayerWalletSystem>();
            var minds = entMan.System<SharedMindSystem>();

            var chemist = entMan.SpawnEntity(HumanProto, map.GridCoords);
            entMan.EnsureComponent<FSFriendlyFireComponent>(chemist);

            var chemistMind = minds.CreateMind(null, "Chemist");
            minds.TransferTo(chemistMind, chemist, mind: entMan.GetComponent<MindComponent>(chemistMind));

            var before = wallet.GetCredits(chemistMind);

            credit.RegisterDelivery(chemist, chemist);
            KillEnemy(entMan, map.GridCoords, chemist);

            Assert.That(wallet.GetCredits(chemistMind), Is.EqualTo(before),
                "self-buffing must not be a way to farm your own kills");
        });
    }

    [Test]
    public async Task ZombiesAreNeverAValidDeliveryTarget()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var credit = entMan.System<FSChemCreditSystem>();
            var wallet = entMan.System<FSPlayerWalletSystem>();
            var minds = entMan.System<SharedMindSystem>();

            var chemist = entMan.SpawnEntity(HumanProto, map.GridCoords);
            var zombie = entMan.SpawnEntity(HumanProto, map.GridCoords);
            entMan.RemoveComponent<FSFriendlyFireComponent>(zombie);

            var chemistMind = minds.CreateMind(null, "Chemist");
            minds.TransferTo(chemistMind, chemist, mind: entMan.GetComponent<MindComponent>(chemistMind));

            var before = wallet.GetCredits(chemistMind);

            credit.RegisterDelivery(zombie, chemist);
            KillEnemy(entMan, map.GridCoords, zombie);

            Assert.That(wallet.GetCredits(chemistMind), Is.EqualTo(before));
        });
    }

    [Test]
    public async Task ReDosingNeverRefillsTheBudget()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var credit = entMan.System<FSChemCreditSystem>();
            var wallet = entMan.System<FSPlayerWalletSystem>();

            var chemist = SpawnChemist(entMan, map.GridCoords, out var chemistMind);
            var soldier = entMan.SpawnEntity(HumanProto, map.GridCoords);
            entMan.EnsureComponent<FSFriendlyFireComponent>(soldier);

            var before = wallet.GetCredits(chemistMind);

            credit.RegisterDelivery(soldier, chemist);
            for (var i = 0; i < 20; i++)
                KillEnemy(entMan, map.GridCoords, soldier);

            var spent = wallet.GetCredits(chemistMind) - before;

            credit.RegisterDelivery(soldier, chemist);
            credit.RegisterDelivery(soldier, chemist, FSMedicalPayoutRates.SplashClaimBudget);
            for (var i = 0; i < 5; i++)
                KillEnemy(entMan, map.GridCoords, soldier);

            Assert.Multiple(() =>
            {
                Assert.That(spent, Is.EqualTo(FSMedicalPayoutRates.ChemClaimBudget),
                    "one crewmate pays out their whole budget and no more");
                Assert.That(wallet.GetCredits(chemistMind) - before, Is.EqualTo(spent),
                    "re-dosing refilled the budget, so one crewmate could be farmed all wave");
            });
        });
    }

    [Test]
    public async Task ASmallerDeliveryKeepsTheLargerBudget()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var credit = entMan.System<FSChemCreditSystem>();
            var wallet = entMan.System<FSPlayerWalletSystem>();

            var chemist = SpawnChemist(entMan, map.GridCoords, out var chemistMind);
            var soldier = entMan.SpawnEntity(HumanProto, map.GridCoords);
            entMan.EnsureComponent<FSFriendlyFireComponent>(soldier);

            var before = wallet.GetCredits(chemistMind);

            credit.RegisterDelivery(soldier, chemist);
            credit.RegisterDelivery(soldier, chemist, FSMedicalPayoutRates.SplashClaimBudget);
            for (var i = 0; i < 20; i++)
                KillEnemy(entMan, map.GridCoords, soldier);

            Assert.That(wallet.GetCredits(chemistMind) - before, Is.EqualTo(FSMedicalPayoutRates.ChemClaimBudget),
                "a cloud after a dart must not shrink what the dart already earned");
        });
    }

    [Test]
    public async Task AWeakenedZombieKilledBySomeoneElsePaysAnAssist()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var wallet = entMan.System<FSPlayerWalletSystem>();

            var chemist = SpawnChemist(entMan, map.GridCoords, out var chemistMind);
            var soldier = entMan.SpawnEntity(HumanProto, map.GridCoords);
            var enemy = SpawnEnemy(entMan, map.GridCoords);
            Tag(entMan, timing, enemy, chemist, 0f, true);

            var before = wallet.GetCredits(chemistMind);
            Kill(entMan, enemy, soldier);

            Assert.That(wallet.GetCredits(chemistMind), Is.GreaterThan(before),
                "a debuff that set up a teammate's kill is the chemist's work and must pay");
        });
    }

    [Test]
    public async Task HeavyChemDamageIsAnAssist()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var wallet = entMan.System<FSPlayerWalletSystem>();

            var chemist = SpawnChemist(entMan, map.GridCoords, out var chemistMind);
            var soldier = entMan.SpawnEntity(HumanProto, map.GridCoords);
            var enemy = SpawnEnemy(entMan, map.GridCoords);
            Tag(entMan, timing, enemy, chemist, 30f, false);

            var before = wallet.GetCredits(chemistMind);
            Kill(entMan, enemy, soldier);

            Assert.That(wallet.GetCredits(chemistMind), Is.GreaterThan(before));
        });
    }

    [Test]
    public async Task AScratchIsNotAnAssist()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var wallet = entMan.System<FSPlayerWalletSystem>();

            var chemist = SpawnChemist(entMan, map.GridCoords, out var chemistMind);
            var soldier = entMan.SpawnEntity(HumanProto, map.GridCoords);
            var enemy = SpawnEnemy(entMan, map.GridCoords);
            Tag(entMan, timing, enemy, chemist, 1f, false);

            var before = wallet.GetCredits(chemistMind);
            Kill(entMan, enemy, soldier);

            Assert.That(wallet.GetCredits(chemistMind), Is.EqualTo(before),
                "a token tick of acid must not earn a share of someone else's kill");
        });
    }

    [Test]
    public async Task YourOwnKillIsNotAnAssist()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var wallet = entMan.System<FSPlayerWalletSystem>();

            var chemist = SpawnChemist(entMan, map.GridCoords, out var chemistMind);
            var enemy = SpawnEnemy(entMan, map.GridCoords);
            Tag(entMan, timing, enemy, chemist, 0f, true);

            var before = wallet.GetCredits(chemistMind);
            Kill(entMan, enemy, chemist);

            Assert.That(wallet.GetCredits(chemistMind), Is.EqualTo(before),
                "the kill already pays the chemist, so an assist on top would pay twice");
        });
    }

    private static EntityUid SpawnChemist(IEntityManager entMan, Robust.Shared.Map.EntityCoordinates coords, out EntityUid mind)
    {
        var minds = entMan.System<SharedMindSystem>();

        var chemist = entMan.SpawnEntity(HumanProto, coords);
        mind = minds.CreateMind(null, "Chemist");
        minds.TransferTo(mind, chemist, mind: entMan.GetComponent<MindComponent>(mind));

        return chemist;
    }

    private static EntityUid SpawnEnemy(IEntityManager entMan, Robust.Shared.Map.EntityCoordinates coords)
    {
        var enemy = entMan.SpawnEntity(EnemyProto, coords);
        entMan.EnsureComponent<WaveSpawnedTagComponent>(enemy);
        return enemy;
    }

    private static void Tag(IEntityManager entMan, IGameTiming timing, EntityUid enemy, EntityUid chemist, float damage, bool debuffed)
    {
        var tag = entMan.EnsureComponent<FSChemTaggedComponent>(enemy);
        tag.Chemist = chemist;
        tag.Until = timing.CurTime + TimeSpan.FromSeconds(10);
        tag.Damage = damage;
        tag.Debuffed = debuffed;
    }

    private static void KillEnemy(IEntityManager entMan, Robust.Shared.Map.EntityCoordinates coords, EntityUid killer)
    {
        Kill(entMan, SpawnEnemy(entMan, coords), killer);
    }

    private static void Kill(IEntityManager entMan, EntityUid enemy, EntityUid killer)
    {
        var state = entMan.GetComponent<MobStateComponent>(enemy);
        var ev = new MobStateChangedEvent(enemy, state, MobState.Alive, MobState.Dead, killer);
        entMan.EventBus.RaiseLocalEvent(enemy, ev, true);
    }
}
