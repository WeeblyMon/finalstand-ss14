using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.NPC;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.FinalStand;

// When the leash gives up on a chase, or a zombie's retaliation runs out, the zombie has to actually stop. A replan
// on its own kept the chase running, since nothing below it ranks higher, and the hunt picked the same player again.
[TestFixture]
[TestOf(typeof(FSLeashSystem))]
public sealed class LeashTest : GameTest
{
    private const string Zombie = "FSZombieNormal";
    private const string Human = "MobHuman";

    [Test]
    public async Task LeashStopsTheChase()
    {
        var (zombie, human) = await Setup(new Vector2(6.5f, 0.5f));
        var entMan = Pair.Server.ResolveDependency<IEntityManager>();

        await WaitUntilChasing(zombie, human);

        // As if the chase had already run past the leash.
        await Pair.Server.WaitPost(() =>
        {
            var bb = entMan.GetComponent<HTNComponent>(zombie).Blackboard;
            var xform = entMan.GetComponent<TransformComponent>(zombie);
            bb.SetValue(FSAIBlackboardKeys.AggroOrigin, new MapCoordinates(new Vector2(-30f, 0f), xform.MapID));
        });

        await Pair.Server.WaitRunTicks(30);

        await Pair.Server.WaitAssertion(() =>
        {
            var bb = entMan.GetComponent<HTNComponent>(zombie).Blackboard;
            Assert.That(bb.TryGetValue<EntityUid>(FSAIBlackboardKeys.LeashedTarget, out var leashed, entMan) && leashed == human,
                "the leash never gave up on the chase");
            Assert.That(IsChasing(entMan, zombie, human), Is.False, "the zombie kept chasing after the leash gave up");
        });
    }

    [Test]
    public async Task ExpiredRetaliationStopsTheChase()
    {
        // Out of the hunt's sight, so only retaliation would go after them.
        var (zombie, human) = await Setup(new Vector2(25.5f, 0.5f));
        var entMan = Pair.Server.ResolveDependency<IEntityManager>();
        var retaliation = entMan.System<FSZombieRetaliationSystem>();

        await Pair.Server.WaitPost(() => retaliation.TryRetaliate(zombie, human));
        await WaitUntilChasing(zombie, human);

        await Pair.Server.WaitPost(() =>
            entMan.GetComponent<HTNComponent>(zombie).Blackboard.SetValue(FSAIBlackboardKeys.RetaliationTimer, 0.05f));

        await Pair.Server.WaitRunTicks(30);

        await Pair.Server.WaitAssertion(() =>
            Assert.That(IsChasing(entMan, zombie, human), Is.False, "the zombie kept chasing after its retaliation ran out"));
    }

    private async Task<(EntityUid Zombie, EntityUid Human)> Setup(Vector2 humanPos)
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid zombie = default;
        EntityUid human = default;
        await server.WaitPost(() =>
        {
            for (var x = -2; x <= 28; x++)
            {
                for (var y = -1; y <= 1; y++)
                    mapSystem.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            zombie = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            human = entMan.SpawnEntity(Human, new EntityCoordinates(map.Grid, humanPos));
        });

        return (zombie, human);
    }

    private async Task WaitUntilChasing(EntityUid zombie, EntityUid human)
    {
        var entMan = Pair.Server.ResolveDependency<IEntityManager>();
        var chasing = false;
        for (var i = 0; i < 20 && !chasing; i++)
        {
            await Pair.Server.WaitRunTicks(3);
            await Pair.Server.WaitPost(() => chasing = IsChasing(entMan, zombie, human));
        }

        Assert.That(chasing, "the zombie never went after the human");
    }

    private static bool IsChasing(IEntityManager entMan, EntityUid zombie, EntityUid human)
    {
        return entMan.TryGetComponent<NPCSteeringComponent>(zombie, out var steering)
               && steering.Coordinates.EntityId == human;
    }
}
