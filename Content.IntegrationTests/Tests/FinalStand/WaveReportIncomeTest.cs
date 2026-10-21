using System.Collections.Generic;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.WaveReport;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// The wave report's EARNED is income. Refunds, sell-backs and transfers go through AddCredits and stay out of it;
// counting them let two players pass the same credits back and forth to inflate each other's total.
[TestFixture]
[TestOf(typeof(FSWaveReportSystem))]
public sealed class WaveReportIncomeTest : GameTest
{
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
            var earned = (Dictionary<EntityUid, int>) typeof(FSWaveReportSystem)
                .GetField("_earned", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(report)!;

            var earner = minds.CreateMind(null, "earner").Owner;
            var receiver = minds.CreateMind(null, "receiver").Owner;

            wallets.GiveCredits(earner, 100);
            wallets.AddCredits(receiver, 100);

            Assert.That(earned.GetValueOrDefault(earner), Is.EqualTo(100), "a kill payout didn't count as earned");
            Assert.That(earned.GetValueOrDefault(receiver), Is.Zero, "a refund or transfer counted as earned");
        });
    }
}
