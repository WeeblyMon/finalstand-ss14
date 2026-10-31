#nullable enable
using System;
using System.Collections;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.NPC;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Damage.Components;
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
    private const string Barricade = "FSBarricadePlasteel";

    // Stuck detection waits 20s per nudge and deletes after the third, so this comfortably covers it.
    private const int Seconds = 110;

    private const int NavmeshTicks = 30;

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

    // A lone zombie boxed in behind a barricade has nowhere to be nudged, and steering's swings at the barricade
    // don't go through melee combat. Breaking a plasteel one takes far longer than the nudges allow, and it must
    // not be deleted for it.
    [Test]
    public async Task ZombieBreakingOutIsNotRemoved()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var htn = entMan.System<HTNSystem>();
        var steering = entMan.System<NPCSteeringSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid zombie = default;
        EntityUid barricade = default;
        await server.WaitPost(() =>
        {
            for (var x = -1; x <= 8; x++)
            {
                for (var y = -1; y <= 1; y++)
                    mapSystem.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            // A one-tile cell at (0, 0), walled in on every side but the east, where the barricade is.
            for (var x = -1; x <= 8; x++)
            {
                entMan.SpawnEntity(Wall, new EntityCoordinates(map.Grid, new Vector2(x + 0.5f, 1.5f)));
                entMan.SpawnEntity(Wall, new EntityCoordinates(map.Grid, new Vector2(x + 0.5f, -0.5f)));
            }

            entMan.SpawnEntity(Wall, new EntityCoordinates(map.Grid, new Vector2(-0.5f, 0.5f)));
            barricade = entMan.SpawnEntity(Barricade, new EntityCoordinates(map.Grid, new Vector2(1.5f, 0.5f)));

            zombie = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<WaveSpawnedTagComponent>(zombie);
            htn.SetHTNEnabled((zombie, entMan.GetComponent<HTNComponent>(zombie)), false);
        });

        // A path asked for before the navmesh is built comes back empty, leaving the zombie on NoPath.
        await server.WaitRunTicks(NavmeshTicks);
        await server.WaitPost(() => steering.Register(zombie, new EntityCoordinates(map.Grid, new Vector2(6.5f, 0.5f))));

        // Watched until it breaks out: past that it stands idle at the steering target, which is rightly stuck.
        var brokeOut = false;
        for (var second = 0; second < Seconds && !brokeOut; second++)
        {
            await Pair.RunSeconds(1);
            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.EntityExists(zombie), $"a zombie breaking out through a barricade was removed as stuck after {second + 1}s");
                brokeOut = !entMan.EntityExists(barricade);
            });
        }

        await server.WaitAssertion(() =>
            Assert.That(brokeOut || entMan.GetComponent<DamageableComponent>(barricade).TotalDamage > 0,
                "the zombie never hit the barricade"));
    }

    // Steering holds a wave zombie still while another one ahead of it is nearer the same target: it's waiting its
    // turn, not stuck. Out of melee range with no crowd round it (the back of a single-file queue), only that hold
    // keeps stuck recovery from nudging it out of the line, or deleting it once nudges can't move it.
    [Test]
    public async Task QueuedZombieIsNotNudged()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var htn = entMan.System<HTNSystem>();
        var steering = entMan.System<NPCSteeringSystem>();
        var timing = server.Timing;
        var map = await Pair.CreateTestMap();

        EntityUid zombie = default;
        await server.WaitPost(() =>
        {
            for (var x = -3; x <= 6; x++)
            {
                for (var y = -3; y <= 3; y++)
                    mapSystem.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            zombie = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<WaveSpawnedTagComponent>(zombie);
            htn.SetHTNEnabled((zombie, entMan.GetComponent<HTNComponent>(zombie)), false);
        });

        await server.WaitRunTicks(NavmeshTicks);
        await server.WaitPost(() => steering.Register(zombie, new EntityCoordinates(map.Grid, new Vector2(5.5f, 0.5f))));

        // Past the first nudge: stuck recovery waits 20s without movement.
        for (var second = 0; second < 30; second++)
        {
            // Steering renews the hold for as long as the one ahead stays put.
            await server.WaitPost(() =>
                entMan.GetComponent<NPCSteeringComponent>(zombie).QueuedUntil = timing.CurTime + TimeSpan.FromSeconds(2));
            await Pair.RunSeconds(1);
        }

        await server.WaitAssertion(() =>
        {
            var states = (IDictionary) typeof(FSStuckRecoverySystem)
                .GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(entMan.System<FSStuckRecoverySystem>())!;

            Assert.That(entMan.EntityExists(zombie), "a zombie waiting its turn in a queue was removed as stuck");
            var state = states[zombie]!;
            var lastNudge = (TimeSpan) state.GetType().GetProperty("LastNudge")!.GetValue(state)!;
            Assert.That(lastNudge, Is.EqualTo(TimeSpan.Zero), "a zombie waiting its turn in a queue was nudged as stuck");
        });
    }
}
