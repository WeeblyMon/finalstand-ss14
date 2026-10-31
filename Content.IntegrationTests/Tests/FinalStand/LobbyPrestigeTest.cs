using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Leveling;
using Content.Shared._FinalStand.Leveling;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// The lobby panel shows PRESTIGE at level 50 from the stored record. A lobby player has no mind, so the request has
// to reset that record; it used to be dropped without a word.
[TestFixture]
[TestOf(typeof(FSLevelingSystem))]
public sealed class LobbyPrestigeTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true };

    [Test]
    public async Task PrestigeFromTheLobbyResetsTheRecord()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var store = server.ResolveDependency<IEntityManager>().System<FSPlayerDataStore>();
        var cEntMan = client.ResolveDependency<IEntityManager>();
        var userId = Pair.Player!.UserId.UserId;

        FSPlayerDataStore.FullRecord original = default;
        await server.WaitPost(() =>
        {
            original = store.GetFullRecord(userId);
            store.UpsertLeveling(userId, 50, 0, 2, "{}");
        });

        await client.WaitPost(() => cEntMan.EntityNetManager!.SendSystemNetworkMessage(new FSPrestigeRequestMessage()));
        await Pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var record = store.GetFullRecord(userId);
            Assert.Multiple(() =>
            {
                Assert.That(record.PrestigeLevel, Is.EqualTo(3), "the lobby prestige didn't go through");
                Assert.That(record.Level, Is.EqualTo(1), "the lobby prestige didn't reset the level");
            });

            store.UpsertLeveling(userId, original.Level, original.Experience, original.PrestigeLevel, original.PrestigeBuffsJson);
        });
    }
}
