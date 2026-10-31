using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Perks;
using Content.Shared._FinalStand.Perks;
using Robust.Server.Player;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// Every perk change is a database write, so a client spamming the shop gets a burst and then one change per interval.
[TestFixture]
[TestOf(typeof(FSPerkSystem))]
public sealed class PerkMutationRateTest : GameTest
{
    // Destructive: the purchases are written to the player's stored data.
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Destructive = true,
    };

    private const int Burst = 8;

    [Test]
    public async Task SpammedPurchasesAreThrottled()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var players = server.ResolveDependency<IPlayerManager>();
        var store = entMan.System<FSPlayerDataStore>();
        var wallets = entMan.System<FSPlayerWalletSystem>();

        var perks = FSPerkDef.All.Keys.ToList();
        Assert.That(perks, Has.Count.GreaterThanOrEqualTo(Burst * 2 + 1), "not enough perks to tell the burst apart");

        await server.WaitPost(() =>
        {
            // Unattached, so the shop range check doesn't apply.
            players.SetAttachedEntity(Pair.Player!, null);
            wallets.GivePerkPoints(Pair.Player!, 1000);
        });

        await SendBuys(perks.Take(Burst * 2));
        await server.WaitAssertion(() => Assert.That(OwnedPerks(store), Is.EqualTo(Burst), "the burst wasn't capped"));

        // The allowance refills over time.
        await Pair.RunTicksSync((int) (server.Timing.TickRate * 2.5f));
        await SendBuys(perks.Skip(Burst * 2).Take(1));
        await server.WaitAssertion(() => Assert.That(OwnedPerks(store), Is.EqualTo(Burst + 1), "the throttle never recovered"));
    }

    private async Task SendBuys(IEnumerable<string> perkIds)
    {
        var cNet = Pair.Client.ResolveDependency<IEntityNetworkManager>();
        await Pair.Client.WaitPost(() =>
        {
            foreach (var id in perkIds)
                cNet.SendSystemNetworkMessage(new FSBuyPerkMessage { PerkId = id });
        });
        await Pair.RunTicksSync(10);
    }

    private int OwnedPerks(FSPlayerDataStore store)
    {
        var (levelsJson, _, _) = store.LoadPerkLoadout(Pair.Player!.UserId.UserId);
        if (string.IsNullOrEmpty(levelsJson))
            return 0;

        var levels = JsonSerializer.Deserialize<Dictionary<string, int>>(levelsJson) ?? new();
        return levels.Count(kv => kv.Value > 0);
    }
}
