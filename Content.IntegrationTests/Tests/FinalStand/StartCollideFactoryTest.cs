using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Weapons.Prediction;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests.FinalStand;

// The lag-compensated hit builds StartCollideEvent through a compiled call to the engine's internal constructor;
// an engine change to that constructor should fail here, not as a silent miss in a fight.
[TestFixture]
public sealed class StartCollideFactoryTest : GameTest
{
    [Test]
    public async Task BuildsTheEventFieldForField()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ours = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var theirs = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var ourBody = entMan.GetComponent<PhysicsComponent>(ours);
            var theirBody = entMan.GetComponent<PhysicsComponent>(theirs);
            var (ourId, ourFixture) = entMan.GetComponent<FixturesComponent>(ours).Fixtures.First();
            var (theirId, theirFixture) = entMan.GetComponent<FixturesComponent>(theirs).Fixtures.First();

            var ev = FSStartCollideFactory.Create(ours, theirs, ourId, theirId, ourFixture, theirFixture, ourBody, theirBody,
                Vector2.UnitY);

            Assert.Multiple(() =>
            {
                Assert.That(ev.OurEntity, Is.EqualTo(ours));
                Assert.That(ev.OtherEntity, Is.EqualTo(theirs));
                Assert.That(ev.OurFixtureId, Is.EqualTo(ourId));
                Assert.That(ev.OtherFixtureId, Is.EqualTo(theirId));
                Assert.That(ev.OurFixture, Is.SameAs(ourFixture));
                Assert.That(ev.OtherFixture, Is.SameAs(theirFixture));
                Assert.That(ev.OurBody, Is.SameAs(ourBody));
                Assert.That(ev.OtherBody, Is.SameAs(theirBody));
                Assert.That(ev.PointCount, Is.Zero);
                Assert.That(ev.WorldNormal, Is.EqualTo(Vector2.UnitY));
            });
        });
    }
}
