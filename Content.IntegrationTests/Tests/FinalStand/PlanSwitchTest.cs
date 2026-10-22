using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.NPC;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.NPC;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.FinalStand;

// Retaliation and breach plans take their target when they start, and a replan never replaces a plan of the same
// rank. Changing the target while one runs has to drop the plan, or the zombie keeps going after the old one.
[TestFixture]
[TestOf(typeof(FSZombieRetaliationSystem))]
[TestOf(typeof(FSBreachTargetSystem))]
public sealed class PlanSwitchTest : GameTest
{
    private const string Zombie = "FSZombieNormal";
    private const string Human = "MobHuman";
    private const string Wall = "WallSolid";
    private const int MaxSwitchTicks = 15;

    [TestCase(true)]
    [TestCase(false)]
    public async Task RetaliationTurnsOnACloserAttacker(bool secondIsCloser)
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid zombie = default;
        EntityUid first = default;
        EntityUid second = default;
        await server.WaitPost(() =>
        {
            for (var x = -2; x <= 28; x++)
            {
                for (var y = -1; y <= 1; y++)
                    mapSystem.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            // Far enough apart that which one is closer is never in doubt while the zombie moves.
            zombie = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            first = entMan.SpawnEntity(Human, new EntityCoordinates(map.Grid, new Vector2(secondIsCloser ? 25.5f : 14.5f, 0.5f)));
            second = entMan.SpawnEntity(Human, new EntityCoordinates(map.Grid, new Vector2(secondIsCloser ? 14.5f : 25.5f, 0.5f)));
        });

        Assert.That(await WaitForTarget(zombie, first, first), "the zombie never went after the first attacker");

        // The second one keeps shooting, so the retaliation never runs out and lets the hunt pick someone instead.
        if (secondIsCloser)
        {
            Assert.That(await WaitForTarget(zombie, second, second), "a closer attacker never took over the chase");
            return;
        }

        Assert.That(await WaitForTarget(zombie, second, second), Is.False, "a farther attacker pulled the zombie off its chase");
        await server.WaitAssertion(() =>
            Assert.That(TargetOf(entMan, zombie), Is.EqualTo(first), "the zombie dropped its chase"));
    }

    [Test]
    public async Task BetterBreachTargetIsHit()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid zombie = default;
        await server.WaitPost(() =>
        {
            for (var x = -3; x <= 3; x++)
            {
                for (var y = -3; y <= 3; y++)
                    mapSystem.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    if (x != 0 || y != 0)
                        entMan.SpawnEntity(Wall, new EntityCoordinates(map.Grid, new Vector2(x + 0.5f, y + 0.5f)));
                }
            }

            zombie = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<WaveSpawnedTagComponent>(zombie);
            // From the middle of the cell a side wall is exactly the default reach away, which the move counts as
            // in range and the attack doesn't; give it the reach to hit any wall of the cell.
            entMan.GetComponent<HTNComponent>(zombie).Blackboard.SetValue("MeleeRange", 1.5f);
        });

        // Boxed in, it picks a wall and starts hitting it.
        EntityUid picked = default;
        for (var i = 0; i < 40 && !picked.IsValid(); i++)
        {
            await server.WaitRunTicks(10);
            await server.WaitPost(() =>
            {
                var bb = entMan.GetComponent<HTNComponent>(zombie).Blackboard;
                if (bb.TryGetValue<EntityUid>(FSAIBlackboardKeys.BreachTarget, out var breach, entMan)
                    && TargetOf(entMan, zombie) == breach)
                {
                    picked = breach;
                }
            });
        }

        Assert.That(picked.IsValid(), "the boxed-in zombie never started on a wall");

        // Any other wall now scores as more than twice as good, so the next rescan switches to it.
        await server.WaitPost(() =>
            entMan.GetComponent<HTNComponent>(zombie).Blackboard.SetValue(FSAIBlackboardKeys.CachedBreachScore, 1e-6f));

        // Left to a replan, the old plan carried on for about three seconds before the new wall was hit.
        int? pickedAt = null;
        int? switchedAt = null;
        for (var tick = 0; tick < 300 && switchedAt == null; tick++)
        {
            await server.WaitRunTicks(1);
            await server.WaitPost(() =>
            {
                var bb = entMan.GetComponent<HTNComponent>(zombie).Blackboard;
                if (!bb.TryGetValue<EntityUid>(FSAIBlackboardKeys.BreachTarget, out var breach, entMan) || breach == picked)
                    return;

                pickedAt ??= tick;
                if (TargetOf(entMan, zombie) == breach)
                    switchedAt = tick;
            });
        }

        Assert.That(pickedAt, Is.Not.Null, "the zombie never picked a better wall");
        Assert.That(switchedAt - pickedAt, Is.LessThanOrEqualTo(MaxSwitchTicks),
            "the zombie kept hitting the old wall after picking a better one");
    }

    // Polls for about two seconds, the shooter hitting the zombie again before each look.
    private async Task<bool> WaitForTarget(EntityUid zombie, EntityUid target, EntityUid shooter)
    {
        var entMan = Pair.Server.ResolveDependency<IEntityManager>();
        var retaliation = entMan.System<FSZombieRetaliationSystem>();
        var found = false;
        for (var i = 0; i < 20 && !found; i++)
        {
            await Pair.Server.WaitPost(() => retaliation.TryRetaliate(zombie, shooter));
            await Pair.Server.WaitRunTicks(3);
            await Pair.Server.WaitPost(() => found = TargetOf(entMan, zombie) == target);
        }

        return found;
    }

    private static EntityUid? TargetOf(IEntityManager entMan, EntityUid zombie)
    {
        var bb = entMan.GetComponent<HTNComponent>(zombie).Blackboard;
        return bb.TryGetValue<EntityUid>("Target", out var target, entMan) ? target : null;
    }
}
