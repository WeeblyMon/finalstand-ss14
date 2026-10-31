#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.RiotShield;
using Content.Shared._FinalStand.RiotShield;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.FinalStand;

// Bulwark's melee-kill mend works on a broken shield too and puts it back into use, rather than leaving it broken
// until prep.
[TestFixture]
[TestOf(typeof(FSRiotShieldSystem))]
public sealed class RiotShieldMendTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task MendingABrokenShieldRepairsIt()
    {
        var entMan = Server.ResolveDependency<IEntityManager>();
        var shields = entMan.System<FSRiotShieldSystem>();

        await Server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity("FSRiotShield", MapCoordinates.Nullspace);
            var shield = entMan.GetComponent<FSRiotShieldComponent>(uid);
            shield.CurrentDurability = 0f;
            shield.IsBroken = true;

            shields.Mend((uid, shield), 0.1f);

            var max = shield.BaseDurability * shield.DurabilityMultiplier;
            Assert.Multiple(() =>
            {
                Assert.That(shield.IsBroken, Is.False, "the mended shield is still broken");
                Assert.That(shield.CurrentDurability, Is.EqualTo(max * 0.1f).Within(0.01f));
            });

            entMan.DeleteEntity(uid);
        });
    }
}
