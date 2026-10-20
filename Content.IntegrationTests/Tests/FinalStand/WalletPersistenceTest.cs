using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Shared._FinalStand.Economy;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// A wallet made by a payout (an observer at a wave clear, an admin ghost) has to start from the player's saved perk
// points, or the next save writes its zero over them.
[TestFixture]
[TestOf(typeof(FSPlayerWalletSystem))]
public sealed class WalletPersistenceTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Destructive = true,
    };

    [Test]
    public async Task PayoutWalletKeepsSavedPerkPoints()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var wallets = entMan.System<FSPlayerWalletSystem>();
        var store = entMan.System<FSPlayerDataStore>();
        var minds = entMan.System<SharedMindSystem>();

        await server.WaitAssertion(() =>
        {
            var session = Pair.Player!;
            store.UpsertPerkPoints(session.UserId.UserId, session.Name, 40);

            // A fresh mind with no wallet, like an observer's.
            var mind = minds.CreateMind(session.UserId, session.Name);
            minds.SetUserId(mind, session.UserId);
            Assert.That(entMan.HasComponent<FSPlayerWalletComponent>(mind), Is.False);

            wallets.DistributeCredits(100);
            wallets.SaveAll();

            Assert.Multiple(() =>
            {
                Assert.That(store.GetPerkPoints(session.UserId.UserId), Is.EqualTo(40), "the payout wallet's save wiped the stored perk points");
                Assert.That(wallets.GetStoredPerkPoints(session.UserId.UserId), Is.EqualTo(40));
                Assert.That(wallets.GetCredits(mind), Is.EqualTo(100));
            });
        });
    }
}
