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
- type: body
  id: TheBodyProto
  name: the body
  root: chest
  slots:
    chest:
      part: ChestHuman
      organs:
        left: LeftHand
        right: RightHand

- type: entity
  id: TheBody
  components:
  - type: Body
    prototype: TheBodyProto
  - type: Hands

- type: entity
  id: LeftHand
  components:
  - type: Organ
  - type: HandOrgan
    handID: left
    data:
      location: Left

- type: entity
  id: RightHand
  components:
  - type: Organ
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
            var bodySystem = entityManager.System<SharedBodySystem>();
            var body = entityManager.SpawnEntity("TheBody", mapData.GridCoords);
            var hands = entityManager.GetComponent<HandsComponent>(body);

            Assert.That(hands.Count, Is.EqualTo(2));

            var handOrgans = bodySystem.GetBodyOrganEntityComps<HandOrganComponent>(body).ToList();
            Assert.That(handOrgans.Count, Is.EqualTo(2));

            var expectedCount = 2;
            foreach (var handOrgan in handOrgans)
            {
                expectedCount--;
                bodySystem.RemoveOrgan(handOrgan.Owner);
                Assert.That(hands.Count, Is.EqualTo(expectedCount));
            }

            bodySystem.TryGetRootPart(body, out var rootPart);
            var chestUid = rootPart!.Value.Owner;

            var protos = new List<string>() { "LeftHand", "RightHand" };
            foreach (var proto in protos)
            {
                expectedCount++;
                var organ = entityManager.SpawnEntity(proto, mapData.GridCoords);
                bodySystem.AddOrganToFirstValidSlot(chestUid, organ);
                Assert.That(hands.Count, Is.EqualTo(expectedCount));
            }
        });
    }
}
