using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.MedicalOps;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ChemSupplyCreditTest : GameTest
{
    private const string HumanProto = "MobHuman";
    private const string ItemProto = "Beaker";

    [Test]
    public async Task PropagatingCarriesTheClaimOutOfTheBottle()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var attribution = entMan.System<FSTreatmentAttributionSystem>();
            var mindSys = entMan.System<SharedMindSystem>();

            var chemist = mindSys.CreateMind(null, "Chemist").Owner;
            var bottle = entMan.SpawnEntity(ItemProto, map.GridCoords);
            var syringe = entMan.SpawnEntity(ItemProto, map.GridCoords);

            entMan.EnsureComponent<FSProducedByComponent>(bottle).ProducerMind = chemist;
            attribution.PropagateProducer(bottle, syringe);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.TryGetComponent<FSProducedByComponent>(syringe, out var carried), Is.True,
                    "drawing from a made bottle must carry the chemist's claim");
                Assert.That(carried!.ProducerMind, Is.EqualTo(chemist));
            });
        });
    }

    [Test]
    public async Task PropagatingFromAnUntaggedContainerTagsNothing()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var attribution = entMan.System<FSTreatmentAttributionSystem>();

            var bottle = entMan.SpawnEntity(ItemProto, map.GridCoords);
            var syringe = entMan.SpawnEntity(ItemProto, map.GridCoords);

            attribution.PropagateProducer(bottle, syringe);

            Assert.That(entMan.HasComponent<FSProducedByComponent>(syringe), Is.False);
        });
    }

    [Test]
    public async Task TheChemistEarnsFromMedicineTheMedicAdministers()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var attribution = entMan.System<FSTreatmentAttributionSystem>();
            var stats = entMan.System<FSMedicalStatsSystem>();
            var damageable = entMan.System<DamageableSystem>();
            var mindSys = entMan.System<SharedMindSystem>();

            var session = ServerSession!;
            if (!mindSys.TryGetMind(session, out var medicMind, out _))
                medicMind = mindSys.CreateMind(session.UserId, "Medic").Owner;

            var medicBody = entMan.SpawnEntity(HumanProto, map.GridCoords);
            mindSys.TransferTo(medicMind, medicBody);

            var patient = entMan.SpawnEntity(HumanProto, map.GridCoords);
            entMan.EnsureComponent<FSMedicalPatientComponent>(patient);

            var chemist = mindSys.CreateMind(null, "Chemist").Owner;
            var syringe = entMan.SpawnEntity(ItemProto, map.GridCoords);
            entMan.EnsureComponent<FSProducedByComponent>(syringe).ProducerMind = chemist;

            attribution.RecordTreatment(patient, medicBody, syringe);

            var brute = new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), 1);
            var before = stats.GetStats(chemist).HealingPoints;

            damageable.TryChangeDamage(patient, brute * 400f, ignoreResistances: true);
            damageable.TryChangeDamage(patient, brute * -60f, ignoreResistances: true, origin: medicBody);

            var earned = stats.GetStats(chemist).HealingPoints - before;
            var medicEarned = stats.GetStats(medicMind).HealingPoints;

            Assert.Multiple(() =>
            {
                Assert.That(earned, Is.GreaterThan(0), "the chemist earned nothing from their own medicine");
                Assert.That(medicEarned, Is.GreaterThan(earned),
                    "supply credit must stay a cut, never more than the medic doing the work");
            });
        });
    }

    [Test]
    public async Task AnUntaggedTreatmentCreditsNoSupplier()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var attribution = entMan.System<FSTreatmentAttributionSystem>();
            var mindSys = entMan.System<SharedMindSystem>();

            var session = ServerSession!;
            if (!mindSys.TryGetMind(session, out var medicMind, out _))
                medicMind = mindSys.CreateMind(session.UserId, "Medic").Owner;

            var medicBody = entMan.SpawnEntity(HumanProto, map.GridCoords);
            mindSys.TransferTo(medicMind, medicBody);

            var patient = entMan.SpawnEntity(HumanProto, map.GridCoords);
            entMan.EnsureComponent<FSMedicalPatientComponent>(patient);

            var syringe = entMan.SpawnEntity(ItemProto, map.GridCoords);
            attribution.RecordTreatment(patient, medicBody, syringe);

            Assert.That(attribution.TryGetAttributedSupplier(patient, out _), Is.False,
                "an untagged item must not create a supply claim");
        });
    }
}
