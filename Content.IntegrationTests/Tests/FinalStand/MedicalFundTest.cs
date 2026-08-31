// The fund is a shared pot with no owner, so nothing else in the round will notice if it drifts.

using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.MedicalOps;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class MedicalFundTest : GameTest
{
    [Test]
    public async Task GrantAndDeductKeepTheBalanceHonest()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var fund = entMan.System<FSMedicalFundSystem>();
            var start = fund.GetBalance();
            var earnedStart = fund.GetLifetimeEarned();

            fund.GrantMedicalFunds(500, "test");

            Assert.Multiple(() =>
            {
                Assert.That(fund.GetBalance(), Is.EqualTo(start + 500));
                Assert.That(fund.GetLifetimeEarned(), Is.EqualTo(earnedStart + 500));
            });

            Assert.That(fund.TryDeductMedicalFunds(fund.GetBalance() + 1), Is.False,
                "the fund must refuse to go negative");
            Assert.That(fund.GetBalance(), Is.EqualTo(start + 500),
                "a refused deduction must not move the balance");

            Assert.That(fund.TryDeductMedicalFunds(500), Is.True);
            Assert.That(fund.GetBalance(), Is.EqualTo(start));

            // Spending is not earning; the department still did the work.
            Assert.That(fund.GetLifetimeEarned(), Is.EqualTo(earnedStart + 500),
                "lifetime earnings must not fall when funds are spent");
        });
    }

    [Test]
    public async Task TheSingletonSurvivesLosingItsHolder()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var fund = entMan.System<FSMedicalFundSystem>();
            fund.GrantMedicalFunds(250, "test");
            Assert.That(fund.GetBalance(), Is.GreaterThan(0));

            var holder = entMan.EntityQueryEnumerator<Content.Shared._FinalStand.MedicalOps.FSMedicalFundComponent>();
            Assert.That(holder.MoveNext(out var holderUid, out _), Is.True);

            entMan.DeleteEntity(holderUid);

            // Re-resolving must mint a fresh pot rather than throwing on the stale cache.
            Assert.That(fund.GetBalance(), Is.EqualTo(0));
            fund.GrantMedicalFunds(100, "test");
            Assert.That(fund.GetBalance(), Is.EqualTo(100));
        });
    }
}
