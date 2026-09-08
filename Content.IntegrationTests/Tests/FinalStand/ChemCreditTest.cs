using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.MedicalOps;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;

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

    private static void KillEnemy(IEntityManager entMan, Robust.Shared.Map.EntityCoordinates coords, EntityUid killer)
    {
        var enemy = entMan.SpawnEntity(EnemyProto, coords);
        entMan.EnsureComponent<WaveSpawnedTagComponent>(enemy);

        var state = entMan.GetComponent<MobStateComponent>(enemy);
        var ev = new MobStateChangedEvent(enemy, state, MobState.Alive, MobState.Dead, killer);
        entMan.EventBus.RaiseLocalEvent(enemy, ev, true);
    }
}
