using System.Collections.Generic;
// Limbs live in the body container, so the woundable graph must follow organ relations.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Medical;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Body;

[TestFixture]
public sealed class WoundableGraphTest : GameTest
{
    [Test]
    public async Task TorsoHasLimbsAsWoundableChildren()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var lookup = entityManager.System<OrganLookupSystem>();
            var body = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);

            Assert.That(lookup.TryGetRootOrgan(body, out var torso), Is.True, "human has no torso organ");

            Assert.That(entityManager.TryGetComponent<WoundableComponent>(torso.Owner, out var torsoWoundable),
                Is.True,
                "torso is not woundable");

            Assert.That(torsoWoundable!.ChildWoundables, Is.Not.Empty,
                "torso has no child woundables - the organ relation graph is not linking them");

            var categories = torsoWoundable.ChildWoundables
                .Select(child => entityManager.GetComponentOrNull<OrganComponent>(child)?.Category)
                .Where(category => category != null)
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(categories, Does.Contain(OrganCategories.Head));
                Assert.That(categories, Does.Contain(OrganCategories.ArmLeft));
                Assert.That(categories, Does.Contain(OrganCategories.ArmRight));
            });

            var leftArm = lookup.EnumerateOrgansOfCategory(body, OrganCategories.ArmLeft).First();
            Assert.That(entityManager.TryGetComponent<WoundableComponent>(leftArm.Owner, out var armWoundable),
                Is.True);

            var handCategories = armWoundable!.ChildWoundables
                .Select(child => entityManager.GetComponentOrNull<OrganComponent>(child)?.Category)
                .ToList();

            Assert.That(handCategories, Does.Contain(OrganCategories.HandLeft));
        });
    }

    [Test]
    public async Task GibbingUnwindsOrganRelations()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var appearance = entityManager.System<Content.Server.Body.Systems.BodyAppearanceSystem>();

            HashSet<EntityUid> gibs = new();
            Assert.DoesNotThrow(() => gibs = appearance.GibBody(body), "gibbing threw while unwinding organs");

            Assert.That(gibs, Is.Not.Empty, "gibbing produced no giblets - organs are not collected");
            Assert.That(entityManager.IsQueuedForDeletion(body), Is.True, "body was not queued for deletion");
        });
    }

    [Test]
    public async Task LimbRemovalAndReinsertionDoesNotThrow()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var lookup = entityManager.System<OrganLookupSystem>();
            var manipulation = entityManager.System<OrganManipulationSystem>();

            var body = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var leg = lookup.EnumerateOrgansOfCategory(body, OrganCategories.LegLeft).First();

            Assert.DoesNotThrow(() => manipulation.RemoveOrgan((leg.Owner, leg.Comp)),
                "removing a leg threw");

            Assert.DoesNotThrow(() => manipulation.InsertOrgan(body, (leg.Owner, leg.Comp)),
                "reinserting a leg threw");
        });
    }

    [Test]
    public async Task SpawningDoesNotChangeMovementSpeed()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapData = await Pair.CreateTestMap();

        EntityUid body = default;

        await server.WaitPost(() => body = entityManager.SpawnEntity("MobHuman", mapData.GridCoords));
        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            EntProtoId human = "MobHuman";
            var expected = protoManager.Index(human)
                .Components.TryGetComponent("MovementSpeedModifier", out var comp)
                ? ((MovementSpeedModifierComponent) comp).BaseWalkSpeed
                : 0f;

            Assert.That(expected, Is.GreaterThan(0f), "test is vacuous - prototype has no walk speed");

            var actual = entityManager.GetComponent<MovementSpeedModifierComponent>(body).BaseWalkSpeed;

            Assert.That(actual, Is.EqualTo(expected).Within(0.01f),
                "a freshly spawned human does not walk at its prototype speed");
        });
    }
}
