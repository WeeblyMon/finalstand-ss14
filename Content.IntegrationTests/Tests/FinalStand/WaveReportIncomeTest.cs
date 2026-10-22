using System.Collections.Generic;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.CashTransfer;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.WaveReport;
using Content.Shared._FinalStand.CashTransfer;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests.FinalStand;

// The wave report's EARNED is income. Refunds, sell-backs and transfers go through AddCredits and stay out of it;
// counting them let two players pass the same credits back and forth to inflate each other's total.
[TestFixture]
[TestOf(typeof(FSWaveReportSystem))]
public sealed class WaveReportIncomeTest : GameTest
{
    // Selling needs a mind that belongs to a player.
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task OnlyEarningsCountAsEarned()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var wallets = entMan.System<FSPlayerWalletSystem>();
        var minds = entMan.System<SharedMindSystem>();
        var report = entMan.System<FSWaveReportSystem>();

        await server.WaitAssertion(() =>
        {
            var earned = Earned(report);

            var earner = minds.CreateMind(null, "earner").Owner;
            var receiver = minds.CreateMind(null, "receiver").Owner;

            wallets.GiveCredits(earner, 100);
            wallets.AddCredits(receiver, 100);

            Assert.That(earned.GetValueOrDefault(earner), Is.EqualTo(100), "a kill payout didn't count as earned");
            Assert.That(earned.GetValueOrDefault(receiver), Is.Zero, "a refund or transfer counted as earned");
        });
    }

    // The paths that hand money back: a transfer between players and selling a weapon to its shop.
    [Test]
    public async Task TransfersAndSellsAreNotEarned()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var wallets = entMan.System<FSPlayerWalletSystem>();
        var minds = entMan.System<SharedMindSystem>();
        var hands = entMan.System<SharedHandsSystem>();
        var report = entMan.System<FSWaveReportSystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var earned = Earned(report);

            var sender = SpawnPlayer(entMan, minds, map.GridCoords, Pair.Player!.UserId, out var senderMind);
            var receiver = SpawnPlayer(entMan, minds, map.GridCoords, null, out var receiverMind);
            wallets.AddCredits(senderMind, 1000);
            entMan.EnsureComponent<FSPlayerWalletComponent>(receiverMind);

            var session = entMan.SpawnEntity("FSCashTransferSession", map.GridCoords);
            var sessionComp = entMan.EnsureComponent<FSCashTransferSessionComponent>(session);
            sessionComp.Target = receiver;
            sessionComp.Opener = sender;
            var transfer = new FSCashTransferRequestMessage(400)
            {
                Actor = sender,
                Entity = entMan.GetNetEntity(session),
                UiKey = FSCashTransferUiKey.Key,
            };
            entMan.EventBus.RaiseLocalEvent(session, (object) transfer, true);

            Assert.That(wallets.GetCredits(receiverMind), Is.EqualTo(400), "the transfer didn't go through");
            Assert.That(earned.GetValueOrDefault(receiverMind), Is.Zero, "credits received in a transfer counted as earned");

            var weapon = entMan.SpawnEntity("WeaponPistolViper", map.GridCoords);
            entMan.EnsureComponent<FSWeaponUpgradeStateComponent>(weapon);
            Assert.That(hands.TryPickupAnyHand(sender, weapon), "couldn't hand the sender the pistol");
            var shop = entMan.SpawnEntity("FSWeaponShopViper", map.GridCoords);
            var sell = new FSShopSellMessage
            {
                Actor = sender,
                Entity = entMan.GetNetEntity(shop),
                UiKey = FSShopWeaponUiKey.Key,
            };
            entMan.EventBus.RaiseLocalEvent(shop, (object) sell, true);

            Assert.That(wallets.GetCredits(senderMind), Is.GreaterThan(600), "the sell-back paid nothing");
            Assert.That(earned.GetValueOrDefault(senderMind), Is.Zero, "a sell-back counted as earned");
        });
    }

    private static Dictionary<EntityUid, int> Earned(FSWaveReportSystem report)
    {
        return (Dictionary<EntityUid, int>) typeof(FSWaveReportSystem)
            .GetField("_earned", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(report)!;
    }

    private static EntityUid SpawnPlayer(IEntityManager entMan, SharedMindSystem minds, EntityCoordinates coords,
        NetUserId? user, out EntityUid mindId)
    {
        var body = entMan.SpawnEntity("MobHuman", coords);
        var mind = minds.CreateMind(user);
        minds.TransferTo(mind, body);
        mindId = mind.Owner;
        return body;
    }
}
