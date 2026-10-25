#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Mobs;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Atmos.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests.FinalStand;

// The flamethrower's fire is plain server data, not entities, but burns like the projectiles it replaced: it sets
// whoever is in the cone alight, passes through mobs, leaves wave zombies alone and stops at walls.
[TestFixture]
[TestOf(typeof(FSFlamethrowerSystem))]
public sealed class FlamethrowerFlameTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    private const string Flamer = "FSZombieFlamethrower";
    private const string Target = "MobHuman";
    private const string Zombie = "FSZombieNormal";
    private const string Wall = "WallSolid";
    private const int FireTicks = 45;

    // A one-tile corridor with the flamer at its west end hosing a target four tiles east.
    private async Task<(EntityUid Flamer, EntityUid Target)> Fire(Action<EntityCoordinates>? between = null)
    {
        var entMan = Server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid flamer = default;
        EntityUid target = default;
        await Server.WaitPost(() =>
        {
            for (var x = -1; x <= 6; x++)
            {
                mapSystem.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            }

            flamer = entMan.SpawnEntity(Flamer, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            target = entMan.SpawnEntity(Target, new EntityCoordinates(map.Grid, new Vector2(4.5f, 0.5f)));
            between?.Invoke(new EntityCoordinates(map.Grid, new Vector2(2.5f, 0.5f)));

            // Straight to firing: it won't pick a target it can't see, and the wall case needs it to try anyway.
            entMan.RemoveComponent<HTNComponent>(flamer);
            var comp = entMan.GetComponent<FSFlamethrowerComponent>(flamer);
            comp.Target = target;
            comp.FiringDirection = Vector2.UnitX;
            comp.IsFiring = true;
            entMan.Dirty(flamer, comp);
        });

        return (flamer, target);
    }

    // The test map is airless, so anything alight goes out again at its next fire update; watch every tick instead
    // of looking once at the end.
    private async Task<HashSet<EntityUid>> RunWatchingFire(int ticks, params EntityUid[] watched)
    {
        var entMan = Server.ResolveDependency<IEntityManager>();
        var alight = new HashSet<EntityUid>();
        for (var i = 0; i < ticks; i++)
        {
            await Server.WaitRunTicks(1);
            await Server.WaitPost(() =>
            {
                foreach (var uid in watched)
                {
                    if (entMan.GetComponent<FlammableComponent>(uid).OnFire)
                        alight.Add(uid);
                }
            });
        }

        return alight;
    }

    [Test]
    public async Task FlamesSetTheTargetAlightWithoutSpawningAnything()
    {
        var entMan = Server.ResolveDependency<IEntityManager>();
        var (flamer, target) = await Fire();

        var bodies = 0;
        void CountBodies(Entity<MetaDataComponent> ent)
        {
            if (entMan.HasComponent<PhysicsComponent>(ent))
                bodies++;
        }

        HashSet<EntityUid> alight;
        entMan.EntityInitialized += CountBodies;
        try
        {
            await Server.WaitRunTicks(5);
            await Server.WaitAssertion(() =>
                Assert.That(entMan.HasComponent<FSFlameStreamComponent>(flamer), "firing put no flames in the air"));
            alight = await RunWatchingFire(FireTicks, target);
        }
        finally
        {
            entMan.EntityInitialized -= CountBodies;
        }

        Assert.Multiple(() =>
        {
            Assert.That(alight, Does.Contain(target), "the target in the cone didn't catch");
            Assert.That(bodies, Is.Zero, "firing spawned physics bodies");
        });
    }

    [Test]
    public async Task AWallStopsTheFlames()
    {
        var entMan = Server.ResolveDependency<IEntityManager>();
        var (flamer, target) = await Fire(at => entMan.SpawnEntity(Wall, at));

        await Server.WaitRunTicks(5);
        await Server.WaitAssertion(() =>
            Assert.That(entMan.HasComponent<FSFlameStreamComponent>(flamer), "firing put no flames in the air"));
        var alight = await RunWatchingFire(FireTicks, target);

        Assert.That(alight, Does.Not.Contain(target), "the flames went through the wall");
    }

    [Test]
    public async Task FlamesPassThroughWaveZombiesWithoutLightingThem()
    {
        var entMan = Server.ResolveDependency<IEntityManager>();
        EntityUid zombie = default;
        var (_, target) = await Fire(at =>
        {
            zombie = entMan.SpawnEntity(Zombie, at);
            entMan.EnsureComponent<WaveSpawnedTagComponent>(zombie);
            entMan.RemoveComponent<HTNComponent>(zombie);
        });

        await Server.WaitAssertion(() =>
            Assert.That(entMan.HasComponent<FlammableComponent>(zombie), "the zombie can't burn at all"));
        var alight = await RunWatchingFire(FireTicks, zombie, target);

        Assert.Multiple(() =>
        {
            Assert.That(alight, Does.Not.Contain(zombie), "the flamer set a wave zombie alight");
            Assert.That(alight, Does.Contain(target), "the flames stopped at the zombie");
        });
    }
}
