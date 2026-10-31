using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.MedicalOps;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// Medical payouts go to a player's medic for treating a player. A body whose player has left the server pays nothing,
// whether the medic patches it up or brings it back.
[TestFixture]
[TestOf(typeof(FSMedicalStatsSystem))]
public sealed class AbandonedPatientPayoutTest : GameTest
{
    // Destructive: it adds a session and drops it again.
    public override PoolSettings PoolSettings => new() { Connected = true, Destructive = true };

    private const string Human = "MobHuman";

    [Test]
    public async Task TopicalsAndRevivesOnALeftPlayerPayNothing()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var minds = entMan.System<SharedMindSystem>();
        var mobState = entMan.System<MobStateSystem>();
        var wallets = entMan.System<FSPlayerWalletSystem>();
        var map = await Pair.CreateTestMap();
        var left = await server.AddDummySession();

        await server.WaitAssertion(() =>
        {
            var medic = entMan.SpawnEntity(Human, map.GridCoords);
            var mind = minds.CreateMind(Pair.Player!.UserId, "medic");
            minds.TransferTo(mind, medic);
            // Topicals only pay medical crew.
            entMan.System<SharedRoleSystem>().MindAddJobRole(mind, silent: true, jobPrototype: "MedicalDoctor");

            var abandoned = entMan.SpawnEntity(Human, map.GridCoords);
            entMan.EnsureComponent<FSMedicalPatientComponent>(abandoned);
            var leftMind = minds.CreateMind(left.UserId, "left the server");
            minds.TransferTo(leftMind, abandoned);
            server.PlayerMan.RemoveSession(left);

            var topical = new FSTopicalAppliedEvent(medic, abandoned);
            entMan.EventBus.RaiseLocalEvent(abandoned, ref topical);
            Assert.That(wallets.GetCredits(mind), Is.Zero, "a topical on a disconnected player's body paid out");

            mobState.ChangeMobState(abandoned, MobState.Dead);
            mobState.ChangeMobState(abandoned, MobState.Critical, origin: medic);
            Assert.That(wallets.GetCredits(mind), Is.Zero, "reviving a disconnected player's body paid out");

            // The same on someone still playing does pay, so the zeroes above aren't just a payout that never runs.
            var patient = entMan.SpawnEntity(Human, map.GridCoords);
            entMan.EnsureComponent<FSMedicalPatientComponent>(patient);
            var onPatient = new FSTopicalAppliedEvent(medic, patient);
            entMan.EventBus.RaiseLocalEvent(patient, ref onPatient);
            var afterTopical = wallets.GetCredits(mind);
            Assert.That(afterTopical, Is.GreaterThan(0), "a topical on a player paid nothing");

            mobState.ChangeMobState(patient, MobState.Dead);
            mobState.ChangeMobState(patient, MobState.Critical, origin: medic);
            Assert.That(wallets.GetCredits(mind), Is.GreaterThan(afterTopical), "reviving a player paid nothing");
        });
    }
}
