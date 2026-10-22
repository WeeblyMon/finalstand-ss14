#nullable enable
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.FinalStand;

// A melee NPC pressed against something it's hitting doesn't move, and steering used to call that stuck: a repath
// after a second, the move failed after three, so the attack dropped and the NPC replanned every few seconds.
[TestFixture]
[TestOf(typeof(NPCSteeringSystem))]
public sealed class MeleeReachTest : GameTest
{
    // A godmode body on an airless map changes only owner-only state, which PVS-off sending logs as an error.
    public override PoolSettings PoolSettings => new() { Connected = false };

    private const string Zombie = "FSZombieNormal";
    private const string Wall = "WallSolid";

    private const int Seconds = 12;
    private const int SampleTicks = 3;

    // Zombie and defender share a walled 2x1 pocket, so neither can step back.
    [Test]
    public async Task KeepsHittingADefenderItCantPush()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var godmode = entMan.System<SharedGodmodeSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid zombie = default;
        await server.WaitPost(() =>
        {
            for (var x = -3; x <= 3; x++)
            {
                for (var y = -3; y <= 3; y++)
                    mapSystem.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            var defender = entMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            godmode.EnableGodmode(defender);

            for (var x = -1; x <= 2; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    if (y == 0 && x is 0 or 1)
                        continue;

                    entMan.SpawnEntity(Wall, new EntityCoordinates(map.Grid, new Vector2(x + 0.5f, y + 0.5f)));
                }
            }

            zombie = SpawnZombie(entMan, new EntityCoordinates(map.Grid, new Vector2(1.5f, 0.5f)));
        });

        await AssertNeverDrops(zombie);
    }

    // The CCC is a 3x3 body, so a zombie at its face is never within the 0.2 that melee steers to.
    [Test]
    public async Task KeepsHittingTheCCC()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid zombie = default;
        await server.WaitPost(() =>
        {
            for (var x = -6; x <= 6; x++)
            {
                for (var y = -6; y <= 6; y++)
                    mapSystem.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            var ccc = entMan.SpawnEntity("FinalStandCCC", new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            zombie = SpawnZombie(entMan, new EntityCoordinates(map.Grid, new Vector2(3.5f, 0.5f)));
            entMan.GetComponent<HTNComponent>(zombie).Blackboard.SetValue(NPCBlackboard.CurrentOrderedTarget, ccc);
        });

        await AssertNeverDrops(zombie);
    }

    // The melee range the wave spawner gives every enemy; the 1.0 default can't plan against a target a tile away.
    private static EntityUid SpawnZombie(IEntityManager entMan, EntityCoordinates coords)
    {
        var zombie = entMan.SpawnEntity(Zombie, coords);
        entMan.EnsureComponent<WaveSpawnedTagComponent>(zombie);
        entMan.GetComponent<HTNComponent>(zombie).Blackboard.SetValue("MeleeRange", 2.0f);
        return zombie;
    }

    private async Task AssertNeverDrops(EntityUid zombie)
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        var engaged = false;
        var drops = 0;
        for (var tick = 0; tick < Seconds * 30; tick += SampleTicks)
        {
            await server.WaitRunTicks(SampleTicks);
            await server.WaitAssertion(() =>
            {
                var fighting = entMan.HasComponent<NPCMeleeCombatComponent>(zombie);
                if (engaged && !fighting)
                    drops++;

                engaged |= fighting;
            });
        }

        Assert.That(engaged, "the zombie never started hitting its target");
        Assert.That(drops, Is.Zero, "the zombie stopped hitting a target it was pressed against");
    }
}
