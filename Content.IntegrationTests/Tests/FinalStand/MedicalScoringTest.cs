// None of the medical scoring rules are visible to the compiler, and every one of them is a payout.

using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.MedicalOps;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class MedicalScoringTest : GameTest
{
    private const string HumanProto = "MobHuman";

    private sealed class Fixture
    {
        public EntityUid Patient;
        public EntityUid MedicBody;
        public EntityUid MedicMind;
        public FSMedicalStatsSystem Stats = default!;
        public DamageableSystem Damageable = default!;
        public DamageSpecifier Brute = default!;

        // The server instance is pooled between tests, so only deltas are meaningful.
        public int Baseline;

        public int Points => Stats.GetStats(MedicMind).HealingPoints - Baseline;

        public void Hurt(float amount)
            => Damageable.TryChangeDamage(Patient, Brute * amount, ignoreResistances: true);

        public void HurtBy(EntityUid origin, float amount)
            => Damageable.TryChangeDamage(Patient, Brute * amount, ignoreResistances: true, origin: origin);

        public void Heal(float amount)
            => Damageable.TryChangeDamage(Patient, Brute * -amount, ignoreResistances: true);

        public void HealBy(EntityUid origin, float amount)
            => Damageable.TryChangeDamage(Patient, Brute * -amount, ignoreResistances: true, origin: origin);
    }

    private Fixture Setup(IEntityManager entMan, IPrototypeManager protos, EntityCoordinates coords,
        bool patientIsTheMedic = false)
    {
        var mindSys = entMan.System<SharedMindSystem>();
        var session = ServerSession!;

        var medicBody = entMan.SpawnEntity(HumanProto, coords);

        // The scoring gate only credits minds backed by a real user, so reuse the test session's.
        if (!mindSys.TryGetMind(session, out var medicMind, out _))
            medicMind = mindSys.CreateMind(session.UserId, "Medic").Owner;

        mindSys.TransferTo(medicMind, medicBody);

        var patient = patientIsTheMedic ? medicBody : entMan.SpawnEntity(HumanProto, coords);
        entMan.EnsureComponent<FSMedicalPatientComponent>(patient);

        var stats = entMan.System<FSMedicalStatsSystem>();

        var f = new Fixture
        {
            Patient = patient,
            MedicBody = medicBody,
            MedicMind = medicMind,
            Stats = stats,
            Damageable = entMan.System<DamageableSystem>(),
            Brute = new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), 1),
        };

        f.Baseline = stats.GetStats(medicMind).HealingPoints;
        return f;
    }

    [Test]
    public async Task HealingPaysAndThenDiminishes()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var f = Setup(entMan, protos, map.GridCoords);

            f.Hurt(400f);

            f.HealBy(f.MedicBody, 60f);
            var first = f.Points;

            f.HealBy(f.MedicBody, 60f);
            var second = f.Points - first;

            f.HealBy(f.MedicBody, 60f);
            var third = f.Points - first - second;

            // The ledger is now past 150, so nothing further from this healer is worth anything.
            f.HealBy(f.MedicBody, 60f);
            var fourth = f.Points - first - second - third;

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.GreaterThan(0), "healing a wounded patient must pay something");
                Assert.That(second, Is.LessThan(first), "the second tier must pay less than the first");
                Assert.That(third, Is.LessThan(second), "payout must keep shrinking toward the cap");
                Assert.That(fourth, Is.EqualTo(0), "past the budget the same healer earns nothing more");
            });
        });
    }

    [Test]
    public async Task HostileDamageRefillsTheBudgetButPlayerDamageDoesNot()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var f = Setup(entMan, protos, map.GridCoords);

            f.Hurt(400f);
            f.HealBy(f.MedicBody, 200f);
            var exhausted = f.Points;

            // Shooting the patient yourself must not reopen the budget.
            f.HurtBy(f.MedicBody, 60f);
            f.HealBy(f.MedicBody, 40f);

            Assert.That(f.Points, Is.EqualTo(exhausted),
                "player-inflicted damage must not reset diminishing returns");

            // A zombie hit carries no player origin, so it should.
            f.Hurt(60f);
            f.HealBy(f.MedicBody, 40f);

            Assert.That(f.Points, Is.GreaterThan(exhausted),
                "hostile damage should restore the full rate");
        });
    }

    [Test]
    public async Task NearFullTargetsAndOverhealPayNothing()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var f = Setup(entMan, protos, map.GridCoords);

            f.Hurt(3f);
            f.HealBy(f.MedicBody, 100f);

            Assert.That(f.Points, Is.EqualTo(0),
                "topping up a near-full patient should not score");

            // Only damage actually present can be undone; the rest is overheal.
            f.Hurt(20f);
            f.HealBy(f.MedicBody, 500f);

            Assert.That(f.Points, Is.LessThanOrEqualTo(25),
                "healing past full must not pay for damage that was never there");
        });
    }

    [Test]
    public async Task ChemHealingIsCreditedToWhoeverAdministeredIt()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var f = Setup(entMan, protos, map.GridCoords);
            var attribution = entMan.System<FSTreatmentAttributionSystem>();

            f.Hurt(400f);

            // An origin-less heal is what reagent metabolism looks like.
            f.Heal(40f);
            Assert.That(f.Points, Is.EqualTo(0), "an unattributed heal should credit nobody");

            attribution.RecordTreatment(f.Patient, f.MedicBody);
            f.Heal(40f);

            Assert.That(f.Points, Is.GreaterThan(0),
                "after administering medicine the medic should own the metabolised healing");
        });
    }

    [Test]
    public async Task SelfTreatmentPaysLessThanTreatingSomeoneElse()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var onSelf = Setup(entMan, protos, map.GridCoords, patientIsTheMedic: true);
            onSelf.Hurt(400f);
            onSelf.HealBy(onSelf.MedicBody, 60f);
            var selfPoints = onSelf.Points;

            var onOther = Setup(entMan, protos, map.GridCoords);
            onOther.Hurt(400f);
            onOther.HealBy(onOther.MedicBody, 60f);
            var otherPoints = onOther.Points;

            Assert.Multiple(() =>
            {
                Assert.That(selfPoints, Is.GreaterThan(0), "self-treatment is still medical work");
                Assert.That(selfPoints, Is.LessThan(otherPoints),
                    "treating someone else must always beat patching yourself up");
            });
        });
    }
}
