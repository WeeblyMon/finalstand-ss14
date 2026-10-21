using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.FinalStand;

// The spacing index is a grid of mob positions rebuilt every tick. Players cover the whole station over a round and
// every round is a new map, so cells nobody stands in any more have to be dropped, not cleared forever.
[TestFixture]
[TestOf(typeof(NPCSteeringSystem))]
public sealed class SpacingIndexPruneTest : GameTest
{
    private const string Zombie = "FSZombieNormal";
    private const string Mob = "MobMouse";
    private const int Mobs = 100;
    private const int Moves = 60;

    [Test]
    public async Task CellsNobodyStandsInArePruned()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var xforms = entMan.System<SharedTransformSystem>();
        var htn = entMan.System<HTNSystem>();
        var steering = entMan.System<NPCSteeringSystem>();
        var godmode = entMan.System<SharedGodmodeSystem>();
        var map = await Pair.CreateTestMap();

        var mobs = new List<EntityUid>();
        await server.WaitPost(() =>
        {
            // One steered wave zombie, so the index is built at all.
            var zombie = entMan.SpawnEntity(Zombie, new MapCoordinates(new Vector2(-10f, -10f), map.MapId));
            entMan.EnsureComponent<WaveSpawnedTagComponent>(zombie);
            htn.SetHTNEnabled((zombie, entMan.GetComponent<HTNComponent>(zombie)), false);
            steering.Register(zombie, new EntityCoordinates(map.MapUid, new Vector2(-20f, -10f)));

            // In godmode, or the vacuum kills them and dead mobs leave the index.
            godmode.EnableGodmode(zombie);
            for (var i = 0; i < Mobs; i++)
            {
                var mob = entMan.SpawnEntity(Mob, new MapCoordinates(new Vector2(0f, i * 3f), map.MapId));
                godmode.EnableGodmode(mob);
                mobs.Add(mob);
            }
        });

        // Every move puts each mob in a cell it has never been in.
        for (var move = 1; move <= Moves; move++)
        {
            var x = move * 3f;
            await server.WaitPost(() =>
            {
                for (var i = 0; i < mobs.Count; i++)
                    xforms.SetWorldPosition(mobs[i], new Vector2(x, i * 3f));
            });
            await server.WaitRunTicks(2);
        }

        await server.WaitAssertion(() =>
        {
            Assert.That(steering.SpacingCellCount, Is.LessThanOrEqualTo(4096 + 2 * (Mobs + 1)),
                $"{Mobs * Moves} cells visited and the index kept them all");
        });
    }
}
