using Content.IntegrationTests.Fixtures;
using Content.Shared.Body;
using Content.Shared.Gibbing;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Body;

[TestFixture]
[TestOf(typeof(GibbableOrganSystem))]
public sealed class GibletTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: GibbingBody
  components:
  - type: Body
  - type: InitialBody
    organs:
      Torso: ChestHuman
      Heart: Giblet
      Liver: Giblet
      Kidneys: Giblet
    relationships:
      Torso: [ Heart, Liver, Kidneys ]

- type: entity
  id: Giblet
  components:
  - type: Organ
  - type: ChildOrgan
  - type: GibbableOrgan
  - type: Physics
";

    [Test]
    public async Task GibletCountTest()
    {
        var pair = Pair;
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("GibbingBody", mapData.GridCoords);
            var gibbing = entityManager.System<GibbingSystem>();
            var giblets = gibbing.Gib(body);

            Assert.That(giblets.Count, Is.EqualTo(3));

            foreach (var giblet in giblets)
            {
                Assert.That(entityManager.HasComponent<GibbableOrganComponent>(giblet), Is.True);
            }
        });
    }
}
