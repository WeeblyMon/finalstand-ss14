using System.Linq;
using System.Numerics;
using System.Reflection;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;

namespace Content.Server._FinalStand.Weapons.Prediction;

public static class FSStartCollideFactory
{
    private static readonly ConstructorInfo Constructor = typeof(StartCollideEvent)
        .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        .Single(c => c.GetParameters().Length == 11);

    private static readonly object EmptyPoints = Activator.CreateInstance(Constructor.GetParameters()[8].ParameterType)!;

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
        return (StartCollideEvent) Constructor.Invoke(
        [
            ourEntity,
            otherEntity,
            ourFixtureId,
            otherFixtureId,
            ourFixture,
            otherFixture,
            ourBody,
            otherBody,
            EmptyPoints,
            0,
            worldNormal,
        ]);
    }
}
