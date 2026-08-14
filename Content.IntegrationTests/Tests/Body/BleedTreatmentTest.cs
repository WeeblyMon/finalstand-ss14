// Topicals, chemicals and rejuvenate must reach woundable bleeds, not just the vanilla bloodstream.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Medical;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Rejuvenate;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Body;

[TestFixture]
public sealed class BleedTreatmentTest : GameTest
{
    [Test]
    public async Task TopicalsAndRejuvenateStopWoundableBleeding()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        EntityUid body = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            var lookup = entityManager.System<OrganLookupSystem>();
            var wounds = entityManager.System<WoundSystem>();

            body = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            torso = lookup.EnumerateOrgansOfCategory(body, OrganCategories.Torso).First().Owner;
            wounds.TryInduceWound(torso, "Slash", 20, out _);
        });

        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var wounds = entityManager.System<WoundSystem>();
            var woundable = entityManager.GetComponent<WoundableComponent>(torso);

            Assert.That(woundable.Bleeds.Float(), Is.GreaterThan(0f),
                "WoundableComponent.Bleeds is not being computed, so per-part bleeding is invisible");
            Assert.That(wounds.IsAnyWoundableBleeding(body), Is.True);

            wounds.TryHealBleedsOnBody(body, -1000f);
            Assert.That(wounds.IsAnyWoundableBleeding(body), Is.False,
                "topicals did not stop woundable bleeding");
        });
    }

    [Test]
    public async Task RejuvenateClearsWounds()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var lookup = entityManager.System<OrganLookupSystem>();
            var wounds = entityManager.System<WoundSystem>();

            var body = entityManager.SpawnEntity("MobHuman", mapData.GridCoords);
            var torso = lookup.EnumerateOrgansOfCategory(body, OrganCategories.Torso).First().Owner;

            wounds.TryInduceWound(torso, "Slash", 20, out _);
            Assert.That(wounds.GetWoundableWounds(torso).Any(), Is.True, "no wound was created");

            entityManager.EventBus.RaiseLocalEvent(body, new RejuvenateEvent());

            var woundable = entityManager.GetComponent<WoundableComponent>(torso);
            Assert.That(woundable.WoundableIntegrity, Is.EqualTo(woundable.IntegrityCap),
                "rejuvenate did not restore the torso");
        });
    }
}
