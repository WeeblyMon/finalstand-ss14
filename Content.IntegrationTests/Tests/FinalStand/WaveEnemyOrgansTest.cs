#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.NPC.HTN;
using Content.Shared.Body;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

// Wave enemies carry no organs. The animal base fills five, each a networked metabolizer that dirties itself every
// second, which at horde sizes is thousands of entity updates a second for nothing.
[TestFixture]
public sealed class WaveEnemyOrgansTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private const string EnemyCompound = "FinalStandEnemyCompound";

    // A human soldier holds its gun, and hands are organs.
    private const string Footsoldier = "FSMobSyndicateFootsoldier";

    [Test]
    public async Task WaveEnemiesSpawnWithoutOrgans()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();
        var map = await Pair.CreateTestMap();

        var enemies = protos.EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract
                && p.ID != Footsoldier
                && p.TryGetComponent<HTNComponent>(out var htn, factory)
                && htn.RootTask.Task == EnemyCompound)
            .Select(p => p.ID)
            .ToList();
        Assert.That(enemies, Is.Not.Empty, "found no wave enemies to check");

        var spawned = new List<(string Id, EntityUid Uid)>();
        await server.WaitPost(() =>
        {
            foreach (var id in enemies)
            {
                spawned.Add((id, entMan.SpawnEntity(id, map.GridCoords)));
            }
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var containers = entMan.System<SharedContainerSystem>();
            Assert.Multiple(() =>
            {
                foreach (var (id, uid) in spawned)
                {
                    var organs = containers.TryGetContainer(uid, BodyComponent.ContainerID, out var container)
                        ? container.ContainedEntities.Count
                        : 0;
                    Assert.That(organs, Is.Zero, $"{id} spawned with organs");
                }
            });
        });
    }
}
