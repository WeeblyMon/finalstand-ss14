using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.MedicalOps.Shop;
using Content.Shared.Storage;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class SyringeShopTest : GameTest
{
    [Test]
    public async Task EveryTierResolvesToARealGun()
    {
        var server = Pair.Server;
        var protos = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var tier in FSSyringeShopDefs.Tiers)
                {
                    Assert.That(protos.HasIndex<EntityPrototype>(tier.SpawnId), Is.True,
                        $"tier '{tier.Id}' advertises {tier.SpawnId}, which does not exist");
                }
            });
        });
    }

    [Test]
    public async Task TiersGetStrictlyBetterAndCostMore()
    {
        var server = Pair.Server;
        var protos = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var ordered = FSSyringeShopDefs.Tiers.OrderBy(t => t.Rank).ToList();

            var baseGun = protos.Index<EntityPrototype>("LauncherSyringe");
            baseGun.TryGetComponent<StorageComponent>("Storage", out var baseStorage);
            var lastCapacity = Capacity(baseStorage);
            var lastPrice = 0;

            Assert.Multiple(() =>
            {
                foreach (var tier in ordered)
                {
                    var proto = protos.Index<EntityPrototype>(tier.SpawnId);

                    Assert.That(proto.TryGetComponent<StorageComponent>("Storage", out var storage), Is.True);
                    Assert.That(Capacity(storage), Is.GreaterThan(lastCapacity),
                        $"tier '{tier.Id}' is not an upgrade on the one below it");
                    Assert.That(tier.Price, Is.GreaterThan(lastPrice),
                        $"tier '{tier.Id}' costs no more than a weaker tier");

                    Assert.That(proto.TryGetComponent<GunComponent>("Gun", out _), Is.True,
                        $"tier '{tier.Id}' is not usable as a gun");

                    lastCapacity = Capacity(storage);
                    lastPrice = tier.Price;
                }
            });
        });
    }

    [Test]
    public async Task RankingPreventsBuyingADowngrade()
    {
        var server = Pair.Server;

        await server.WaitAssertion(() =>
        {
            var mk2 = FSSyringeShopDefs.GetTier("mk2");
            var mk3 = FSSyringeShopDefs.GetTier("mk3");

            Assert.Multiple(() =>
            {
                Assert.That(mk2, Is.Not.Null);
                Assert.That(mk3, Is.Not.Null);
                Assert.That(FSSyringeShopDefs.RankOf("mk3"), Is.GreaterThan(FSSyringeShopDefs.RankOf("mk2")));
                Assert.That(FSSyringeShopDefs.RankOf(null), Is.EqualTo(0), "owning nothing must rank below every tier");
            });
        });
    }

    private static int Capacity(StorageComponent? storage)
    {
        if (storage == null)
            return 0;

        var total = 0;
        foreach (var box in storage.Grid)
            total += (box.Width + 1) * (box.Height + 1);

        return total;
    }
}
