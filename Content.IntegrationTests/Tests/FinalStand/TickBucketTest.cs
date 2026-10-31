using System.Linq;
using Content.Server._FinalStand.NPC;
using Content.Server.NPC.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// Per-zombie work is spread over ticks by bucket. Spawned zombies get uids a fixed stride apart (the zombie plus its
// child entities), and whatever that stride is, the horde has to split evenly across the buckets.
[TestFixture]
[TestOf(typeof(FSTickBucket))]
public sealed class TickBucketTest
{
    private const int Horde = 3000;

    private static readonly int[] Strides = { 1, 2, 3, 4, 5, 6, 8, 16 };

    private static EntityUid[] Spawned(int stride)
    {
        return Enumerable.Range(0, Horde).Select(i => new EntityUid(15545 + i * stride)).ToArray();
    }

    // Steering every other tick (2), breach (3), leash and obstacle smash (8).
    [Test]
    public void AnySpawnStrideSplitsEvenly([ValueSource(nameof(Strides))] int stride, [Values(2, 3, 8)] int buckets)
    {
        var counts = new int[buckets];
        foreach (var uid in Spawned(stride))
            counts[FSTickBucket.Of(uid, buckets)]++;

        var even = Horde / buckets;
        Assert.That(counts, Has.All.InRange(even * 0.85, even * 1.15), $"lopsided buckets: {string.Join(", ", counts)}");
    }

    // Each breach bucket spreads its zombies' rescans over all ten turns, not a few of them.
    [Test]
    public void BreachRescansSpreadOverEveryTurn([ValueSource(nameof(Strides))] int stride)
    {
        const int buckets = 3;
        const int turns = 10;
        var counts = new int[buckets, turns];
        foreach (var uid in Spawned(stride))
        {
            var (bucket, turn) = FSBreachTargetSystem.Slot(uid);
            counts[bucket, turn]++;
        }

        var even = Horde / (buckets * turns);
        Assert.That(counts.Cast<int>(), Has.All.InRange(even * 0.6, even * 1.4), "some rescan turns are crowded, others empty");
    }

    // Zombies steering on the same tick still pass on both sides.
    [Test]
    public void SteeringSideIsIndependentOfTick([ValueSource(nameof(Strides))] int stride)
    {
        foreach (var slot in new[] { 0, 1 })
        {
            var onTick = Spawned(stride).Where(u => NPCSteeringSystem.SteerSlot(u) == slot).ToArray();
            var left = onTick.Count(NPCSteeringSystem.PrefersLeft);
            Assert.That(left, Is.InRange(onTick.Length * 0.4, onTick.Length * 0.6),
                $"zombies steering on slot {slot} mostly pass on the same side");
        }
    }
}
