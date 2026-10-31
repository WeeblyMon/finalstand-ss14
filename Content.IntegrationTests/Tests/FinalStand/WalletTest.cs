using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class WalletTest : GameTest
{
    // A negative charge used to pass the balance check and pay out (the armour shop's downgrade did exactly that).
    [Test]
    public async Task NegativeChargeIsRefused()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var wallet = entMan.System<FSPlayerWalletSystem>();
            var mind = entMan.System<SharedMindSystem>().CreateMind(null, "Spender").Owner;

            wallet.GiveCredits(mind, 100);
            Assert.Multiple(() =>
            {
                Assert.That(wallet.TryDeductCredits(mind, -30_000), Is.False);
                Assert.That(wallet.GetCredits(mind), Is.EqualTo(100));
                Assert.That(wallet.TryDeductCredits(mind, 60), Is.True);
                Assert.That(wallet.GetCredits(mind), Is.EqualTo(40));
            });
        });
    }
}
