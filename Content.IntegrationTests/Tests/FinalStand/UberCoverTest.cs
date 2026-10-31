using System;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.MedicalOps;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

// An ÜberCharge covers the medic and whoever the beam is on. Moving the beam to someone else ends the last
// patient's cover, or a medic clicking round the crew makes everyone untouchable for the whole uber.
[TestFixture]
[TestOf(typeof(FSUberSystem))]
public sealed class UberCoverTest : GameTest
{
    // No client. On an airless test map an ubered body's only change can be its owner-only suffocation alert, since
    // the uber blocks the damage, and sending all states to a client that doesn't own it logs that as an error.
    public override PoolSettings PoolSettings => new() { Connected = false };

    private const string Human = "MobHuman";
    private const string Gun = "FSMedicalBeamGunUberCharger";

    [Test]
    public async Task SwitchingPatientsEndsTheLastOnesCover()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var medigun = entMan.System<FSMediGunSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid first = default;
        EntityUid second = default;
        await server.WaitPost(() =>
        {
            var medic = entMan.SpawnEntity(Human, map.GridCoords);
            first = entMan.SpawnEntity(Human, map.GridCoords);
            second = entMan.SpawnEntity(Human, map.GridCoords);
            entMan.EnsureComponent<FSFriendlyFireComponent>(first);
            entMan.EnsureComponent<FSFriendlyFireComponent>(second);

            var gun = entMan.SpawnEntity(Gun, map.GridCoords);
            var comp = entMan.GetComponent<FSMediGunComponent>(gun);
            comp.UberEndTime = timing.CurTime + TimeSpan.FromSeconds(10);

            medigun.Link((gun, comp), first, medic);
            Assert.That(entMan.HasComponent<FSUberedComponent>(first), "the linked patient wasn't covered");

            // One link at a time, so this drops the first patient.
            medigun.Link((gun, comp), second, medic);
            Assert.That(entMan.HasComponent<FSUberedComponent>(second), "the new patient wasn't covered");
        });

        // Past the short grace a dropped patient keeps.
        await server.WaitRunTicks(30);

        await server.WaitAssertion(() =>
            Assert.That(entMan.HasComponent<FSUberedComponent>(first), Is.False, "the dropped patient kept the cover"));
    }

    // A medic in their own uber, beamed by another uber medic, keeps their own cover: when that beam moves on,
    // whichever of the two ubers runs longer, and when the other uber runs out first with the beam still on them.
    // Their own gun may have dropped its links in the meantime, which clears who holds it but not whose uber it is.
    [TestCase(5, true, false)]
    [TestCase(20, true, false)]
    [TestCase(1, false, false)]
    [TestCase(20, true, true)]
    public async Task OwnUberOutlastsAnotherMedicsBeam(int otherSeconds, bool beamMovesOn, bool ownLinksDropped)
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var medigun = entMan.System<FSMediGunSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid ownMedic = default;
        TimeSpan ownEnd = default;
        await server.WaitPost(() =>
        {
            ownMedic = SpawnMedic(entMan, map.GridCoords);
            var otherMedic = entMan.SpawnEntity(Human, map.GridCoords);
            var patient = entMan.SpawnEntity(Human, map.GridCoords);
            entMan.EnsureComponent<FSFriendlyFireComponent>(ownMedic);
            entMan.EnsureComponent<FSFriendlyFireComponent>(patient);

            var ownGun = entMan.SpawnEntity(Gun, map.GridCoords);
            var ownComp = entMan.GetComponent<FSMediGunComponent>(ownGun);
            Deploy(entMan, (ownGun, ownComp), ownMedic);
            ownEnd = ownComp.UberEndTime!.Value;
            if (ownLinksDropped)
                medigun.Release((ownGun, ownComp));

            var otherGun = entMan.SpawnEntity(Gun, map.GridCoords);
            var otherComp = entMan.GetComponent<FSMediGunComponent>(otherGun);
            otherComp.UberEndTime = timing.CurTime + TimeSpan.FromSeconds(otherSeconds);
            otherComp.Medic = otherMedic;

            medigun.Link((otherGun, otherComp), ownMedic, otherMedic);
            if (beamMovesOn)
                medigun.Link((otherGun, otherComp), patient, otherMedic);
        });

        // Past the other uber's end and the grace a dropped patient keeps.
        await server.WaitRunTicks(90);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<FSUberedComponent>(ownMedic, out var ubered),
                "the medic lost their own uber's cover when another medic's beam moved on");
            Assert.That(ubered.EndTime, Is.GreaterThanOrEqualTo(ownEnd), "the medic's cover ends before their own uber does");
        });
    }

    // A charged uber handed to another medic mid-uber covers the patients they beam, never the new holder: the cover
    // belongs to whoever popped it.
    [Test]
    public async Task PassedOnUberDoesNotCoverItsNewHolder()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var medigun = entMan.System<FSMediGunSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid holder = default;
        await server.WaitPost(() =>
        {
            var deployer = SpawnMedic(entMan, map.GridCoords);
            holder = SpawnMedic(entMan, map.GridCoords);
            var otherMedic = entMan.SpawnEntity(Human, map.GridCoords);
            var patient = entMan.SpawnEntity(Human, map.GridCoords);
            var otherPatient = entMan.SpawnEntity(Human, map.GridCoords);
            entMan.EnsureComponent<FSFriendlyFireComponent>(holder);
            entMan.EnsureComponent<FSFriendlyFireComponent>(patient);
            entMan.EnsureComponent<FSFriendlyFireComponent>(otherPatient);

            var gun = entMan.SpawnEntity(Gun, map.GridCoords);
            var comp = entMan.GetComponent<FSMediGunComponent>(gun);
            Deploy(entMan, (gun, comp), deployer);
            medigun.Release((gun, comp));
            medigun.Link((gun, comp), patient, holder);

            // Another uber medic's beam passes over the new holder and moves on.
            var otherGun = entMan.SpawnEntity(Gun, map.GridCoords);
            var otherComp = entMan.GetComponent<FSMediGunComponent>(otherGun);
            otherComp.UberEndTime = timing.CurTime + TimeSpan.FromSeconds(10);
            otherComp.Medic = otherMedic;
            medigun.Link((otherGun, otherComp), holder, otherMedic);
            medigun.Link((otherGun, otherComp), otherPatient, otherMedic);
        });

        // Past the grace a dropped patient keeps.
        await server.WaitRunTicks(30);

        await server.WaitAssertion(() =>
            Assert.That(entMan.HasComponent<FSUberedComponent>(holder), Is.False,
                "the medic holding someone else's uber was covered by it"));
    }

    private static EntityUid SpawnMedic(IEntityManager entMan, EntityCoordinates coords)
    {
        var minds = entMan.System<SharedMindSystem>();
        var medic = entMan.SpawnEntity(Human, coords);
        var mind = minds.CreateMind(null, "medic");
        minds.TransferTo(mind, medic);
        entMan.System<SharedRoleSystem>().MindAddJobRole(mind, silent: true, jobPrototype: "MedicalDoctor");
        return medic;
    }

    // Pops the uber the way the action does, so the gun records whose uber it is.
    private static void Deploy(IEntityManager entMan, Entity<FSMediGunComponent> gun, EntityUid medic)
    {
        gun.Comp.UberCharge = FSUberSystem.FullCharge;
        entMan.EventBus.RaiseLocalEvent(gun, new FSMediGunUberActionEvent { Performer = medic });
        Assert.That(gun.Comp.UberActive, "the medic couldn't pop the uber");
    }
}
