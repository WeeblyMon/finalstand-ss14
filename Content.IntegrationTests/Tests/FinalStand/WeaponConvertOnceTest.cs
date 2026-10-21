using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Research;
using Content.Server._FinalStand.Shop;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

// A conversion swaps the held weapon for a new one carrying its upgrades. Two requests landing on the same tick must
// not both find the old weapon and hand out two upgraded copies.
[TestFixture]
[TestOf(typeof(FSShopWeaponSystem))]
public sealed class WeaponConvertOnceTest : GameTest
{
    // Destructive: the test unlocks every research node, which recycling a pair doesn't undo.
    public override PoolSettings PoolSettings => new() { Destructive = true };

    private static readonly EntProtoId Shop = "FSWeaponShopHarvester";
    private static readonly EntProtoId Weapon = "WeaponHarvesterFS";
    private static readonly EntProtoId Converted = "WeaponXrayCannonFS";
    private const string Conversion = "harvester-xray-conversion";
    private const int Funds = 1_000_000;

    [Test]
    public async Task SameTickConvertsGiveOneWeapon()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var minds = entMan.System<SharedMindSystem>();
        var wallets = entMan.System<FSPlayerWalletSystem>();
        var research = entMan.System<FSResearchSystem>();
        var access = entMan.System<SharedAccessSystem>();
        var hands = entMan.System<SharedHandsSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid mindId = default;
        await server.WaitPost(() =>
        {
            research.UnlockAllNodes();

            var player = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var mind = minds.CreateMind(null, "converter");
            minds.TransferTo(mind, player);
            mindId = mind.Owner;
            wallets.GiveCredits(mindId, Funds);

            // The harvester shop is science only.
            access.TrySetTags(player, ["Research"], entMan.EnsureComponent<AccessComponent>(player));

            var weapon = entMan.SpawnEntity(Weapon, map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(player, weapon), "couldn't hand the player the weapon");

            var shop = entMan.SpawnEntity(Shop, map.GridCoords);
            for (var i = 0; i < 2; i++)
            {
                var msg = new FSShopUpgradeMessage(Conversion)
                {
                    Actor = player,
                    Entity = entMan.GetNetEntity(shop),
                    UiKey = FSShopWeaponUiKey.Key,
                };
                entMan.EventBus.RaiseLocalEvent(shop, (object) msg, true);
            }
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var converted = 0;
            var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out _, out var meta))
            {
                if (meta.EntityPrototype?.ID == Converted.Id)
                    converted++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(converted, Is.EqualTo(1), "two same-tick conversions duplicated the weapon");
                Assert.That(wallets.GetCredits(mindId), Is.GreaterThan(0).And.LessThan(Funds), "the conversion wasn't charged");
            });
        });
    }
}
