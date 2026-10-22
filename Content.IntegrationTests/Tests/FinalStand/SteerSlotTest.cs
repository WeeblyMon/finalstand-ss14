using System.Linq;
using Content.Server.NPC.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// Wave zombies steer every other tick, half of them on each. Spawned zombies get uids a fixed stride apart (the
// zombie plus its child entities), and whatever that stride is, the horde has to split evenly across both ticks and
// both passing sides, and the two choices must not line up.
[TestFixture]
[TestOf(typeof(NPCSteeringSystem))]
public sealed class SteerSlotTest
{
    private const int Horde = 1000;

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(8)]
    [TestCase(16)]
    public void AnySpawnStrideSplitsEvenly(int stride)
    {
        var uids = Enumerable.Range(0, Horde).Select(i => new EntityUid(15545 + i * stride)).ToArray();

        foreach (var slot in new[] { 0, 1 })
        {
            var onTick = uids.Where(u => NPCSteeringSystem.SteerSlot(u) == slot).ToArray();
            Assert.That(onTick, Has.Length.InRange(Horde * 0.45, Horde * 0.55), $"slot {slot} is lopsided");

            var left = onTick.Count(NPCSteeringSystem.PrefersLeft);
            Assert.That(left, Is.InRange(onTick.Length * 0.4, onTick.Length * 0.6),
                $"zombies steering on slot {slot} mostly pass on the same side");
        }
    }
}
