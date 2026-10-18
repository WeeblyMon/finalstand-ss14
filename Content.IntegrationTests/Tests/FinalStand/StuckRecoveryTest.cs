#nullable enable
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.FinalStand;

// A zombie that stands still because it's busy attacking must not be treated as stuck and removed.
// Zombie and defender share a walled 2x1 pocket, so the zombie can't move, like one pressed against the CCC.
[TestFixture]
public sealed class StuckRecoveryTest : GameTest
{
    private const string Zombie = "FSZombieNormal";
    private const string Defender = "MobHuman";
    private const string Wall = "WallSolid";

    // Stuck detection waits 20s per nudge and deletes after the third, so this comfortably covers it.
    private const int Seconds = 110;

    [Test]
    public async Task FightingZombieIsNotRemoved()
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
                {
                    mapSystem.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            var defender = entMan.SpawnEntity(Defender, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
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

            zombie = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(1.5f, 0.5f)));
            entMan.EnsureComponent<WaveSpawnedTagComponent>(zombie);
        });

        await Pair.RunSeconds(Seconds);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(zombie), "a zombie attacking a defender was removed as stuck");
            Assert.That(entMan.HasComponent<NPCMeleeCombatComponent>(zombie), "the zombie should still be fighting");
        });
    }
}
