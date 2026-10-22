using System;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.MedicalOps;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Shared.GameObjects;
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
    [TestCase(5, true)]
    [TestCase(20, true)]
    [TestCase(1, false)]
    public async Task OwnUberOutlastsAnotherMedicsBeam(int otherSeconds, bool beamMovesOn)
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var medigun = entMan.System<FSMediGunSystem>();
        var uber = entMan.System<FSUberSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid ownMedic = default;
        TimeSpan ownEnd = default;
        await server.WaitPost(() =>
        {
            ownMedic = entMan.SpawnEntity(Human, map.GridCoords);
            var otherMedic = entMan.SpawnEntity(Human, map.GridCoords);
            var patient = entMan.SpawnEntity(Human, map.GridCoords);
            entMan.EnsureComponent<FSFriendlyFireComponent>(ownMedic);
            entMan.EnsureComponent<FSFriendlyFireComponent>(patient);

            ownEnd = timing.CurTime + TimeSpan.FromSeconds(10);
            var ownGun = entMan.SpawnEntity(Gun, map.GridCoords);
            var ownComp = entMan.GetComponent<FSMediGunComponent>(ownGun);
            ownComp.UberEndTime = ownEnd;
            ownComp.ParentEntity = ownMedic;
            uber.Cover((ownGun, ownComp), ownMedic);

            var otherGun = entMan.SpawnEntity(Gun, map.GridCoords);
            var otherComp = entMan.GetComponent<FSMediGunComponent>(otherGun);
            otherComp.UberEndTime = timing.CurTime + TimeSpan.FromSeconds(otherSeconds);
            otherComp.ParentEntity = otherMedic;

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
}
