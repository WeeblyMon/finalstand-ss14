using System;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.NPC.Systems;
using Content.Shared.NPC;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.FinalStand;

// Wave zombie spacing adds danger four directions at a time. It has to land on exactly the numbers the one-at-a-time
// loop did, including directions straight across or square to the push, and a neighbour whose position went bad.
[TestFixture]
[TestOf(typeof(NPCSteeringSystem))]
public sealed class SteeringDangerLanesTest : GameTest
{
    private static readonly Vector2[] NonFinite =
    {
        new(float.NaN, float.NaN),
        new(float.NaN, 0.5f),
        new(0.5f, float.NaN),
        new(float.PositiveInfinity, 0f),
        new(float.NegativeInfinity, 0.3f),
        new(float.PositiveInfinity, float.NegativeInfinity),
    };

    [Test]
    public async Task LanesMatchTheScalarLoop()
    {
        var server = Pair.Server;
        // The lanes are filled when the system starts.
        server.ResolveDependency<IEntityManager>().System<NPCSteeringSystem>();

        await server.WaitAssertion(() =>
        {
            var random = new Random(1234);
            var lanes = new float[SharedNPCSteeringSystem.InterestDirections];
            var scalar = new float[SharedNPCSteeringSystem.InterestDirections];
            var mismatches = 0;

            for (var run = 0; run < 20000; run++)
            {
                for (var i = 0; i < lanes.Length; i++)
                {
                    lanes[i] = scalar[i] = run % 5 == 0 ? 0f : (float) random.NextDouble();
                }

                // Every steering direction itself and the angles half way between, a few non-finite pushes, then
                // anything at all.
                var toward = run < 48
                    ? new Angle(run * SharedNPCSteeringSystem.InterestRadians / 2).ToVec()
                    : run < 48 + NonFinite.Length
                        ? NonFinite[run - 48]
                        : new Angle(random.NextDouble() * Math.Tau).ToVec() * (float) (0.2 + random.NextDouble());
                var weight = (float) (random.NextDouble() * 3);

                for (var push = 0; push < 3; push++)
                {
                    NPCSteeringSystem.AddDanger(lanes, toward, weight);
                    for (var i = 0; i < scalar.Length; i++)
                    {
                        var dot = Vector2.Dot(toward, NPCSteeringSystem.Directions[i]);
                        if (dot > 0f)
                            scalar[i] = MathF.Max(scalar[i], dot * weight);
                    }

                    toward = new Vector2(-toward.Y, toward.X);
                }

                for (var i = 0; i < lanes.Length; i++)
                {
                    if (BitConverter.SingleToInt32Bits(lanes[i]) != BitConverter.SingleToInt32Bits(scalar[i]))
                        mismatches++;
                }
            }

            Assert.That(mismatches, Is.Zero, "the four-wide danger update didn't match the scalar loop");
        });
    }
}
