using System.Threading.Tasks;
using Content.Server.Destructible;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ShieldDestructibleTest
{
    [Test]
    public async Task FsRiotShieldHasNoDestructionThreshold()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity("FSRiotShield", MapCoordinates.Nullspace);
            var count = entMan.TryGetComponent<DestructibleComponent>(uid, out var destructible)
                ? destructible.Thresholds.Count
                : 0;

            Assert.That(count, Is.Zero,
                $"FSRiotShield has {count} destruction threshold(s); damage will delete it instead of breaking it.");

            entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }
}
