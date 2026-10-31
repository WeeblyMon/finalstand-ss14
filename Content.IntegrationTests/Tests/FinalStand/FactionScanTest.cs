#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.FinalStand;

// Hostile scans check faction members directly when there are few of them; this covers that path.
[TestFixture]
public sealed class FactionScanTest : GameTest
{
    private const string Zombie = "FSZombie";
    private const string Player = "FSPlayer";

    [Test]
    public async Task NearbyHostilesTrackMembership()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var factions = entMan.System<NpcFactionSystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            EntityUid At(float x, string faction)
            {
                var uid = entMan.SpawnEntity(null, new MapCoordinates(new Vector2(x, 0f), map.MapId));
                factions.AddFaction(uid, faction);
                return uid;
            }

            var zombie = At(0f, Zombie);
            var near = At(3f, Player);
            var far = At(8f, Player);
            var ally = At(1f, Zombie);

            var hostiles = factions.GetNearbyHostiles(zombie, 5f).ToList();
            Assert.That(hostiles, Is.EquivalentTo(new[] { near }));
            Assert.That(hostiles, Does.Not.Contain(far));
            Assert.That(hostiles, Does.Not.Contain(ally));

            factions.RemoveFaction(near, Player);
            Assert.That(factions.GetNearbyHostiles(zombie, 5f), Is.Empty);

            factions.AddFaction(near, Player);
            entMan.DeleteEntity(near);
            Assert.That(factions.GetNearbyHostiles(zombie, 5f), Is.Empty);

            entMan.DeleteEntity(far);
            entMan.DeleteEntity(ally);
            entMan.DeleteEntity(zombie);
        });
    }
}
