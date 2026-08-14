// A severed limb has to carry the body's sprite identity, and the body has to hide the layer it lost.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Medical;
using Content.Shared._Shitmed.Body.Part;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Body;

[TestFixture]
public sealed class LimbAppearanceTest : GameTest
{
    [Test]
    public async Task SeveredLimbKeepsTheBodySpriteIdentity()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var lookup = entityManager.System<OrganLookupSystem>();
            var manipulation = entityManager.System<OrganManipulationSystem>();

            var body = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);

            Assert.That(entityManager.TryGetComponent<HumanoidProfileComponent>(body, out var profile), Is.True,
                "human has no humanoid profile");

            var leftArm = lookup.EnumerateOrgansOfCategory(body, OrganCategories.ArmLeft).First();

            manipulation.RemoveOrgan(leftArm.Owner);

            Assert.That(entityManager.TryGetComponent<BodyPartAppearanceComponent>(leftArm.Owner, out var appearance),
                Is.True,
                "severed arm has no appearance component - it will render as a generic limb");

            Assert.Multiple(() =>
            {
                Assert.That(appearance!.Type, Is.EqualTo(HumanoidVisualLayers.LArm),
                    "severed arm resolved to the wrong sprite layer");

                Assert.That(appearance.Species, Is.EqualTo(profile!.Species),
                    "severed arm did not inherit the body's species sprite set");

                Assert.That(appearance.ID, Is.Not.Null,
                    "severed arm has no base layer id - nothing to draw");
            });
        });
    }

    [Test]
    public async Task DetachingALimbHidesItsLayerOnTheBody()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var lookup = entityManager.System<OrganLookupSystem>();
            var manipulation = entityManager.System<OrganManipulationSystem>();

            var body = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var profile = entityManager.GetComponent<HumanoidProfileComponent>(body);

            Assert.That(profile.PermanentlyHidden, Does.Not.Contain(HumanoidVisualLayers.LArm),
                "arm layer was hidden before the arm was even removed");

            var leftArm = lookup.EnumerateOrgansOfCategory(body, OrganCategories.ArmLeft).First();
            manipulation.RemoveOrgan(leftArm.Owner);

            Assert.That(profile.PermanentlyHidden, Does.Contain(HumanoidVisualLayers.LArm),
                "body still draws the arm it no longer has");

            Assert.That(profile.PermanentlyHidden, Does.Contain(HumanoidVisualLayers.LHand),
                "the hand went with the arm but its layer is still drawn");
        });
    }
}
