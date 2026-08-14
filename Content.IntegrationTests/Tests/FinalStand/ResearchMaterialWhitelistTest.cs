// An empty material whitelist rejects everything, so the console must contribute the materials its nodes cost.

using Content.IntegrationTests.Fixtures;
using Content.Shared.Materials;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ResearchMaterialWhitelistTest : GameTest
{
    [Test]
    public async Task ResearchConsoleAcceptsTheMaterialsItsNodesCost()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var materials = entityManager.System<SharedMaterialStorageSystem>();
            var console = entityManager.SpawnEntity("FSResearchComputer", mapData.GridCoords);

            materials.UpdateMaterialWhitelist(console);

            Assert.That(entityManager.TryGetComponent<MaterialStorageComponent>(console, out var storage), Is.True,
                "research console has no material storage");

            Assert.That(storage!.MaterialWhiteList, Is.Not.Empty,
                "console stored an empty whitelist - it will reject every material a linked silo offers");

            Assert.Multiple(() =>
            {
                Assert.That(materials.IsMaterialWhitelisted((console, storage), "Steel"), Is.True);
                Assert.That(materials.IsMaterialWhitelisted((console, storage), "Plastic"), Is.True);
                Assert.That(materials.IsMaterialWhitelisted((console, storage), "Gold"), Is.True);
            });
        });
    }
}
