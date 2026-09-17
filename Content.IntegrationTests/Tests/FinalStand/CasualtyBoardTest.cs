using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.MedicalOps;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class CasualtyBoardTest : GameTest
{
    private const string HumanProto = "MobHuman";

    [Test]
    public async Task AHurtCallerAppearsOnTheBoard()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var casualty = entMan.System<FSCasualtySystem>();
            var damageable = entMan.System<DamageableSystem>();

            var patient = entMan.SpawnEntity(HumanProto, map.GridCoords);
            damageable.TryChangeDamage(patient,
                new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), 20),
                ignoreResistances: true);

            casualty.RegisterCall(patient);

            var board = casualty.BuildBoard();
            Assert.That(board.Entries.Select(e => entMan.GetEntity(e.Patient)), Does.Contain(patient));
        });
    }

    [Test]
    public async Task AHealthyCallerSurvivesTheNextTick()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        var patient = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            var casualty = entMan.System<FSCasualtySystem>();
            patient = entMan.SpawnEntity(HumanProto, map.GridCoords);
            casualty.RegisterCall(patient);
        });

        await Pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var casualty = entMan.System<FSCasualtySystem>();
            var board = casualty.BuildBoard();

            Assert.That(board.Entries.Select(e => entMan.GetEntity(e.Patient)), Does.Contain(patient),
                "an undamaged player's deliberate call was pruned as 'recovered' on the next tick");
        });
    }

    [Test]
    public async Task HealingAHurtCallerClearsTheCall()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        var patient = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            var casualty = entMan.System<FSCasualtySystem>();
            var damageable = entMan.System<DamageableSystem>();

            patient = entMan.SpawnEntity(HumanProto, map.GridCoords);
            damageable.TryChangeDamage(patient,
                new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), 20),
                ignoreResistances: true);

            casualty.RegisterCall(patient);
            damageable.ClearAllDamage(patient);
        });

        await Pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var casualty = entMan.System<FSCasualtySystem>();
            var board = casualty.BuildBoard();

            Assert.That(board.Entries.Select(e => entMan.GetEntity(e.Patient)), Does.Not.Contain(patient),
                "a fully healed patient should drop off the board");
        });
    }

    [Test]
    public async Task GoingCriticalRegistersACallWithoutPressingAnything()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        var session = ServerSession;
        Assert.That(session, Is.Not.Null);

        var patient = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            patient = entMan.SpawnEntity(HumanProto, map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, patient);
        });

        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var casualty = entMan.System<FSCasualtySystem>();
            var damageable = entMan.System<DamageableSystem>();

            var brute = new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), 25);
            for (var i = 0; i < 20; i++)
                damageable.TryChangeDamage(patient, brute, ignoreResistances: true);

            var board = casualty.BuildBoard();
            Assert.That(board.Entries.Select(e => entMan.GetEntity(e.Patient)), Does.Contain(patient),
                "a player who was downed without pressing Call Medic never reaches the board");
        });
    }

    [Test]
    public async Task ADownedNonPlayerIsIgnored()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var casualty = entMan.System<FSCasualtySystem>();
            var damageable = entMan.System<DamageableSystem>();

            var mob = entMan.SpawnEntity(HumanProto, map.GridCoords);

            var brute = new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), 25);
            for (var i = 0; i < 20; i++)
                damageable.TryChangeDamage(mob, brute, ignoreResistances: true);

            Assert.That(casualty.BuildBoard().Entries, Is.Empty,
                "NPCs going down would flood the board every wave");
        });
    }

    [Test]
    public async Task ACallGivesThePatientAWaitingStatus()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var casualty = entMan.System<FSCasualtySystem>();
            var patient = entMan.SpawnEntity(HumanProto, map.GridCoords);

            casualty.RegisterCall(patient);

            Assert.That(entMan.TryGetComponent<FSCasualtyStatusComponent>(patient, out var status), Is.True,
                "a called patient needs the status component or the downed HUD cannot show anything");
            Assert.That(status!.Responder, Is.Null, "nobody has responded yet");
        });
    }

    [Test]
    public async Task RespondingNamesTheMedicOnThePatient()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var casualty = entMan.System<FSCasualtySystem>();
            var patient = entMan.SpawnEntity(HumanProto, map.GridCoords);
            var medic = entMan.SpawnEntity(HumanProto, map.GridCoords);

            casualty.RegisterCall(patient);
            casualty.Respond(medic, patient);

            var status = entMan.GetComponent<FSCasualtyStatusComponent>(patient);
            Assert.That(status.Responder, Is.Not.Null,
                "the patient's HUD reads Responder - if this is null the en-route line never appears");
        });
    }

    [Test]
    public async Task OnlyMedicalJobsGetTheBoardAction()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var roles = entMan.System<FSMedicalRosterSystem>();

            Assert.Multiple(() =>
            {
                foreach (var job in new[] { "MedicalDoctor", "ChiefMedicalOfficer", "CombatMedic", "Chemist" })
                {
                    Assert.That(roles.IsMedicalJob(job), Is.True, $"{job} should get the casualty board");
                }

                foreach (var job in new[] { "Captain", "SecurityOfficer", "StationEngineer", "Scientist" })
                {
                    Assert.That(roles.IsMedicalJob(job), Is.False,
                        $"{job} is not medical staff - all-access IDs must not hand out the casualty board");
                }

                Assert.That(roles.IsMedicalJob(null), Is.False);
            });
        });
    }

    [Test]
    public async Task ADeletedPatientDropsOffTheBoard()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var casualty = entMan.System<FSCasualtySystem>();
            var patient = entMan.SpawnEntity(HumanProto, map.GridCoords);

            casualty.RegisterCall(patient);
            entMan.DeleteEntity(patient);

            Assert.That(casualty.BuildBoard().Entries, Is.Empty);
        });
    }
}
