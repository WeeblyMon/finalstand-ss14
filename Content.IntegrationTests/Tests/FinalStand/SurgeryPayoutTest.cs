using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.MedicalOps;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// Finishing a surgery on a player pays the surgeon. The completion is raised on the patient, so the payout has to
// listen there; a body that isn't a player's pays nothing.
[TestFixture]
[TestOf(typeof(FSMedicalStatsSystem))]
public sealed class SurgeryPayoutTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true };

    private const string Human = "MobHuman";

    [Test]
    public async Task SurgeryOnAPlayerPaysTheSurgeon()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var minds = entMan.System<SharedMindSystem>();
        var wallets = entMan.System<FSPlayerWalletSystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var surgeon = entMan.SpawnEntity(Human, map.GridCoords);
            // The payout goes to a player's mind.
            var mind = minds.CreateMind(Pair.Player!.UserId, "surgeon");
            minds.TransferTo(mind, surgeon);

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
}
