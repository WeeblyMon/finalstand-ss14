using Content.Shared._FinalStand.Medical;
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Body;

[TestFixture]
[TestOf(typeof(HandOrganSystem))]
public sealed class HandOrganTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TheBody
  components:
  - type: Body
  - type: InitialBody
    organs:
      Torso: ChestHuman
      HandLeft: LeftHand
      HandRight: RightHand
    relationships:
      Torso: [ HandLeft, HandRight ]
  - type: Hands

- type: entity
  id: LeftHand
  components:
  - type: Organ
    category: HandLeft
  - type: ChildOrgan
  - type: HandOrgan
    handID: left
    data:
      location: Left

- type: entity
  id: RightHand
  components:
  - type: Organ
    category: HandRight
  - type: ChildOrgan
  - type: HandOrgan
    handID: right
    data:
      location: Right
";
    [Test]
    public async Task HandInsertionAndRemovalTest()
    {
        var pair = Pair;
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var lookup = entityManager.System<OrganLookupSystem>();
            var manipulation = entityManager.System<OrganManipulationSystem>();
            var body = entityManager.SpawnEntity("TheBody", mapData.GridCoords);
            var hands = entityManager.GetComponent<HandsComponent>(body);

            Assert.That(hands.Count, Is.EqualTo(2));

            lookup.TryGetBodyOrgans<HandOrganComponent>(body, out var handOrgans);
            Assert.That(handOrgans.Count, Is.EqualTo(2));

            var expectedCount = 2;
            foreach (var handOrgan in handOrgans)
            {
                expectedCount--;
                manipulation.RemoveOrgan(handOrgan.Owner);
                Assert.That(hands.Count, Is.EqualTo(expectedCount));
            }

            lookup.TryGetRootOrgan(body, out var rootPart);
            var chestUid = rootPart.Owner;

            var protos = new List<string>() { "LeftHand", "RightHand" };
            foreach (var proto in protos)
            {
                expectedCount++;
                var organ = entityManager.SpawnEntity(proto, mapData.GridCoords);
                manipulation.InsertOrgan(body, organ, chestUid);
                Assert.That(hands.Count, Is.EqualTo(expectedCount));
            }
        });
    }
}
