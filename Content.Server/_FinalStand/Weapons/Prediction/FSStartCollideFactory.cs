using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;

namespace Content.Server._FinalStand.Weapons.Prediction;

// StartCollideEvent's constructor is internal to the engine. It's compiled into a delegate once;
// invoking it through reflection boxed every argument on every lag-compensated hit.
public static class FSStartCollideFactory
{
    private static readonly Func<EntityUid, EntityUid, string, string, Fixture, Fixture, PhysicsComponent, PhysicsComponent,
        Vector2, StartCollideEvent> Construct = Compile();

    public static StartCollideEvent Create(
        EntityUid ourEntity,
        EntityUid otherEntity,
        string ourFixtureId,
        string otherFixtureId,
        Fixture ourFixture,
        Fixture otherFixture,
        PhysicsComponent ourBody,
        PhysicsComponent otherBody,
        Vector2 worldNormal)
    {
        return Construct(ourEntity, otherEntity, ourFixtureId, otherFixtureId, ourFixture, otherFixture, ourBody, otherBody,
            worldNormal);
    }

    private static Func<EntityUid, EntityUid, string, string, Fixture, Fixture, PhysicsComponent, PhysicsComponent, Vector2,
        StartCollideEvent> Compile()
    {
        var constructor = typeof(StartCollideEvent)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Single(c => c.GetParameters().Length == 11);
        var parameters = constructor.GetParameters();

        var ourEntity = Expression.Parameter(typeof(EntityUid));
        var otherEntity = Expression.Parameter(typeof(EntityUid));
        var ourFixtureId = Expression.Parameter(typeof(string));
        var otherFixtureId = Expression.Parameter(typeof(string));
        var ourFixture = Expression.Parameter(typeof(Fixture));
        var otherFixture = Expression.Parameter(typeof(Fixture));
        var ourBody = Expression.Parameter(typeof(PhysicsComponent));
        var otherBody = Expression.Parameter(typeof(PhysicsComponent));
        var worldNormal = Expression.Parameter(typeof(Vector2));

        // No contact points: the swept hit has none, and nothing downstream reads them.
        var body = Expression.New(constructor,
            ourEntity,
            otherEntity,
            ourFixtureId,
            otherFixtureId,
            ourFixture,
            otherFixture,
            ourBody,
            otherBody,
            Expression.Default(parameters[8].ParameterType),
            Expression.Constant(0),
            worldNormal);

        return Expression.Lambda<Func<EntityUid, EntityUid, string, string, Fixture, Fixture, PhysicsComponent,
                PhysicsComponent, Vector2, StartCollideEvent>>(body,
                ourEntity, otherEntity, ourFixtureId, otherFixtureId, ourFixture, otherFixture, ourBody, otherBody, worldNormal)
            .Compile();
    }
}
