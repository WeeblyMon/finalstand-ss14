using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Medical.Effects;
using Content.Shared.Flash.Components;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// An organ that grants a component the body already had mustn't take the body's own copy with it when it comes out.
[TestFixture]
public sealed class OrganEffectTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: FSTestOrganBody
  components:
  - type: Tag

- type: entity
  id: FSTestGrantingOrgan
  components:
  - type: OrganEffect
    onAdd:
    - type: Tag
    - type: FlashImmunity
";

    [Test]
    public async Task RemovalKeepsWhatTheBodyAlreadyHad()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var effects = entMan.System<OrganEffectSystem>();
            var body = entMan.SpawnEntity("FSTestOrganBody", map.GridCoords);
            var organ = entMan.SpawnEntity("FSTestGrantingOrgan", map.GridCoords);
            var effect = entMan.GetComponent<OrganEffectComponent>(organ);

            effects.AddComponents(body, (organ, effect), effect.OnAdd!);
            Assert.That(entMan.HasComponent<FlashImmunityComponent>(body));

            effects.RemoveComponents(body, (organ, effect), effect.OnAdd!);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<TagComponent>(body), "the body's own component went with the organ");
                Assert.That(entMan.HasComponent<FlashImmunityComponent>(body), Is.False, "the granted component stayed");
            });
        });
    }
}
