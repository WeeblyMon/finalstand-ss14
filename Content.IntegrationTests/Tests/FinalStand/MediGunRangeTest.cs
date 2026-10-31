using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.MedicalOps;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Wieldable;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.FinalStand;

// A click picks a patient near the cursor, and the medigun only links one its beam can reach. A link past the
// beam's range broke on the next tick and took the patient's uber cover with it.
[TestFixture]
[TestOf(typeof(FSMediGunSystem))]
public sealed class MediGunRangeTest : GameTest
{
    private const string Human = "MobHuman";
    private const string Gun = "FSMedicalBeamGunUberCharger";

    [Test]
    public async Task ClickOnlyLinksPatientsInRange()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var hands = entMan.System<SharedHandsSystem>();
        var wield = entMan.System<SharedWieldableSystem>();
        var minds = entMan.System<SharedMindSystem>();
        var roles = entMan.System<SharedRoleSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid medic = default;
        EntityUid gun = default;
        EntityUid far = default;
        EntityUid near = default;
        await server.WaitAssertion(() =>
        {
            // The medigun only answers to medical crew.
            medic = entMan.SpawnEntity(Human, new MapCoordinates(Vector2.Zero, map.MapId));
            var mind = minds.CreateMind(null, "medic");
            minds.TransferTo(mind, medic);
            roles.MindAddJobRole(mind, silent: true, jobPrototype: "MedicalDoctor");
            gun = entMan.SpawnEntity(Gun, new MapCoordinates(Vector2.Zero, map.MapId));
            Assert.That(hands.TryPickupAnyHand(medic, gun), "couldn't hand the medic the medigun");
            Assert.That(wield.TryWield(gun, medic), "couldn't wield the medigun");

            var maxRange = entMan.GetComponent<FSMediGunComponent>(gun).Range;
            far = SpawnPatient(entMan, map.MapId, maxRange + 2f);
            near = SpawnPatient(entMan, map.MapId, maxRange - 2f);
        });

        // Wielding starts the gun's use delay; only the range should decide below.
        await server.WaitRunTicks((int) (server.Timing.TickRate * 1.5f));

        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<FSMediGunComponent>(gun);
            Click(entMan, medic, gun, far);
            Assert.That(comp.Patients, Does.Not.Contain(far), "linked a patient past the beam's range");

            Click(entMan, medic, gun, near);
            Assert.That(comp.Patients, Does.Contain(near), "didn't link a patient in range");
        });
    }

    private static EntityUid SpawnPatient(IEntityManager entMan, MapId mapId, float distance)
    {
        var patient = entMan.SpawnEntity(Human, new MapCoordinates(new Vector2(distance, 0f), mapId));
        entMan.EnsureComponent<FSFriendlyFireComponent>(patient);
        return patient;
    }

    private static void Click(IEntityManager entMan, EntityUid user, EntityUid gun, EntityUid target)
    {
        var ev = new AfterInteractEvent(user, gun, target, entMan.GetComponent<TransformComponent>(target).Coordinates, true);
        entMan.EventBus.RaiseLocalEvent(gun, ev);
    }
}
