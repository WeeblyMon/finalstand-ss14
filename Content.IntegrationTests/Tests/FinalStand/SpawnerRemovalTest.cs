using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.FinalStand;

// A secondary spawner only runs alongside a primary on its lane, so deleting a lane's last primary mid-wave takes
// its secondaries out of the active list too.
[TestFixture]
[TestOf(typeof(WaveEnemySpawningSystem))]
public sealed class SpawnerRemovalTest : GameTest
{
    [Test]
    public async Task DeletingALanesLastPrimaryDropsItsSecondaries()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var rule = entMan.AddComponent<WaveGameRuleComponent>(entMan.SpawnEntity(null, MapCoordinates.Nullspace));

            EntityUid Spawner(string lane, bool secondary)
            {
                var uid = entMan.SpawnEntity(null, map.GridCoords);
#pragma warning disable RA0002
                var spawner = entMan.AddComponent<WaveEnemySpawnerComponent>(uid);
                spawner.DirectionLabel = lane;
                spawner.Secondary = secondary;
                rule.SpawnerEntities.Add(uid);
#pragma warning restore RA0002
                return uid;
            }

            var north = Spawner("N", false);
            var northSecondary = Spawner("N", true);
            var south = Spawner("S", false);
            var southBackup = Spawner("S", false);
            var southSecondary = Spawner("S", true);

            entMan.DeleteEntity(north);
            entMan.DeleteEntity(south);

            Assert.That(rule.SpawnerEntities, Is.EquivalentTo(new[] { southBackup, southSecondary }),
                "only the lane that lost its last primary should lose its secondary");

            entMan.DeleteEntity(northSecondary);
            entMan.DeleteEntity(southBackup);
            entMan.DeleteEntity(southSecondary);
            entMan.DeleteEntity(rule.Owner);
        });
    }
}
