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
}
