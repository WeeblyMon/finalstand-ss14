// Organs live inside the patient's container, which silently cancelled every surgery do-after.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Medical;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Body;

[TestFixture]
public sealed class SurgeryStepTest : GameTest
{
    [Test]
    public async Task IncisionStepStarts()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var lookup = entityManager.System<OrganLookupSystem>();
            var surgerySystem = entityManager.System<SharedSurgerySystem>();
            var standing = entityManager.System<StandingStateSystem>();
            var hands = entityManager.System<SharedHandsSystem>();

            var patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var scalpel = entityManager.SpawnEntity("Scalpel", mapData.GridCoords);

            standing.Down(patient);
            Assert.That(hands.TryPickupAnyHand(surgeon, scalpel), Is.True, "surgeon could not hold the scalpel");

            var arm = lookup.EnumerateOrgansOfCategory(patient, OrganCategories.ArmLeft).First();

            var started = surgerySystem.TryDoSurgeryStep(patient,
                arm.Owner,
                surgeon,
                "SurgeryOpenIncision",
                "SurgeryStepOpenIncisionScalpel",
                out var reason);

            Assert.That(started, Is.True, $"incision step refused: {reason}");
        });
    }

    [Test]
    public async Task AmputationDetachesTheLimb()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var lookup = entityManager.System<OrganLookupSystem>();
            var wounds = entityManager.System<WoundSystem>();

            var patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var arm = lookup.EnumerateOrgansOfCategory(patient, OrganCategories.ArmLeft).First();

            Assert.That(lookup.TryGetParentOrgan(arm.Owner, out var torso), Is.True, "arm has no parent organ");

            wounds.AmputateWoundableSafely(torso, arm.Owner, amputateChildrenSafely: true);

            Assert.That(lookup.EnumerateOrgansOfCategory(patient, OrganCategories.ArmLeft).Any(),
                Is.False,
                "arm is still attached after amputation");
        });
    }

    [Test]
    public async Task AttachSurgeryIsOfferedAfterAmputation()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var lookup = entityManager.System<OrganLookupSystem>();
            var wounds = entityManager.System<WoundSystem>();
            var surgery = entityManager.System<SharedSurgerySystem>();
            var standing = entityManager.System<StandingStateSystem>();

            var patient = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var surgeon = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            standing.Down(patient);

            var arm = lookup.EnumerateOrgansOfCategory(patient, OrganCategories.ArmLeft).First();
            Assert.That(lookup.TryGetParentOrgan(arm.Owner, out var torso), Is.True);
            wounds.AmputateWoundableSafely(torso, arm.Owner, amputateChildrenSafely: true);

            var hands = entityManager.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(surgeon, arm.Owner), Is.True, "surgeon could not hold the arm");

            surgery.TryDoSurgeryStep(patient,
                torso,
                surgeon,
                "SurgeryAttachLeftArm",
                "SurgeryStepInsertFeature",
                out var reason);

            Assert.That(reason,
                Is.Not.EqualTo(StepInvalidReason.SurgeryInvalid),
                "attach-arm surgery is not valid on the torso after amputation - its conditions reject it");

            Assert.That(reason,
                Is.Not.EqualTo(StepInvalidReason.MissingTool),
                "the severed arm is not accepted as the tool for its own reattachment");
        });
    }
}
