using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.Sprite;
using Content.IntegrationTests.Fixtures;
using Robust.Client.GameObjects;
using Robust.Client.State;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.FinalStand;

// The fade only click-tests the sprites under the player when one of them can fade at all. A tree over the player
// still has to fade with that check in front of it.
[TestFixture]
[TestOf(typeof(SpriteFadeSystem))]
public sealed class SpriteFadeShortcutTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true };

    [Test]
    public async Task TreeOverThePlayerFades()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        EntityUid tree = default;
        await server.WaitPost(() =>
        {
            tree = sEntMan.SpawnEntity("FloraTree", map.GridCoords);
            // Under the canopy, which is drawn about a tile above the trunk, and clear of the trunk's fixture.
            var player = sEntMan.SpawnEntity("MobHuman",
                new EntityCoordinates(map.Grid, map.GridCoords.Position + new Vector2(0f, 0.9f)));
            server.PlayerMan.SetAttachedEntity(Pair.Player!, player);
        });

        await Pair.RunTicksSync(10);
        await client.WaitPost(() =>
        {
            var states = client.ResolveDependency<IStateManager>();
            if (states.CurrentState is not GameplayState)
                states.RequestStateChange<GameplayState>();
        });

        // Long enough for the alpha to come well down; it falls at one a second to 0.4.
        await Pair.RunTicksSync(30);

        await client.WaitAssertion(() =>
        {
            var sprite = client.EntMan.GetComponent<SpriteComponent>(Pair.ToClientUid(tree));
            Assert.That(sprite.Color.A, Is.LessThan(0.9f), "the tree over the player never faded");
        });
    }
}
