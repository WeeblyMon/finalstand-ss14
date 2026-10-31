using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Pings;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.FinalStand;

// A ping only keeps its target when the target is at the ping, so a client can't tag along something it can't
// see. A fast enemy moves on before the request lands, so the slack grows with its speed and the sender's ping.
[TestFixture]
[TestOf(typeof(FSPingSystem))]
public sealed class PingTargetTest : GameTest
{
    [Test]
    public async Task SlackGrowsWithTargetSpeed()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var pings = entMan.System<FSPingSystem>();
        var physics = entMan.System<SharedPhysicsSystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var target = entMan.SpawnEntity("FSZombieNormal", new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            var near = new EntityCoordinates(map.Grid, new Vector2(2.5f, 0.5f));
            var far = new EntityCoordinates(map.Grid, new Vector2(6.5f, 0.5f));
            var body = entMan.GetComponent<PhysicsComponent>(target);

            Assert.Multiple(() =>
            {
                Assert.That(pings.IsTargetAtPing(target, near, 100), "a standing target at the ping was dropped");
                Assert.That(pings.IsTargetAtPing(target, far, 100), Is.False, "a standing target 6 tiles off was kept");
            });

            physics.SetLinearVelocity(target, new Vector2(10f, 0f), body: body);

            Assert.Multiple(() =>
            {
                Assert.That(pings.IsTargetAtPing(target, far, 100), "a fast target that moved on was dropped");
                Assert.That(pings.IsTargetAtPing(target, new EntityCoordinates(map.Grid, new Vector2(12.5f, 0.5f)), 100),
                    Is.False, "the slack has no bound");
            });

            entMan.DeleteEntity(target);
        });
    }
}
