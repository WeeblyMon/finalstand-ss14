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

// Finishing a surgery on a player pays the surgeon. The completion is raised on the patient, so the payout has to
// listen there; a body that isn't a player's pays nothing, and neither does one whose player has left the server.
// Nothing else a medic does pays on that body either.
[TestFixture]
[TestOf(typeof(FSMedicalStatsSystem))]
public sealed class SurgeryPayoutTest : GameTest
{
    // Destructive: it adds a session and drops it again.
    public override PoolSettings PoolSettings => new() { Connected = true, Destructive = true };

    private const string Human = "MobHuman";

    [Test]
    public async Task SurgeryOnAPlayerPaysTheSurgeon()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var minds = entMan.System<SharedMindSystem>();
        var wallets = entMan.System<FSPlayerWalletSystem>();
        var map = await Pair.CreateTestMap();
        var left = await server.AddDummySession();

        await server.WaitAssertion(() =>
        {
            var surgeon = entMan.SpawnEntity(Human, map.GridCoords);
            // The payout goes to a player's mind.
            var mind = minds.CreateMind(Pair.Player!.UserId, "surgeon");
            minds.TransferTo(mind, surgeon);

            var abandoned = entMan.SpawnEntity(Human, map.GridCoords);
            entMan.EnsureComponent<FSMedicalPatientComponent>(abandoned);
            var leftMind = minds.CreateMind(left.UserId, "left the server");
            minds.TransferTo(leftMind, abandoned);
            server.PlayerMan.RemoveSession(left);
            var onAbandoned = new FSSurgeryCompletedEvent(surgeon, abandoned, abandoned, EntityUid.Invalid);
            entMan.EventBus.RaiseLocalEvent(abandoned, ref onAbandoned);
            Assert.That(wallets.GetCredits(mind), Is.Zero, "surgery on a disconnected player's body paid out");

            var corpse = entMan.SpawnEntity(Human, map.GridCoords);
            var onCorpse = new FSSurgeryCompletedEvent(surgeon, corpse, corpse, EntityUid.Invalid);
            entMan.EventBus.RaiseLocalEvent(corpse, ref onCorpse);
            Assert.That(wallets.GetCredits(mind), Is.Zero, "surgery on a body that isn't a player's paid out");

            var patient = entMan.SpawnEntity(Human, map.GridCoords);
            entMan.EnsureComponent<FSMedicalPatientComponent>(patient);
            var onPatient = new FSSurgeryCompletedEvent(surgeon, patient, patient, EntityUid.Invalid);
            entMan.EventBus.RaiseLocalEvent(patient, ref onPatient);
            Assert.That(wallets.GetCredits(mind), Is.GreaterThan(0), "a finished surgery on a player paid nothing");
        });
    }

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
