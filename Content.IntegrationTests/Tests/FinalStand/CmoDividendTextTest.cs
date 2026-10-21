using System.Globalization;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.MedicalOps;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests.FinalStand;

// The Dividend tooltip and its radio announcement quote what it costs and pays. They said $1,200 while it paid
// $1,800, so the text is checked against the numbers the ability actually uses.
[TestFixture]
[TestOf(typeof(FSCmoAbilitySystem))]
public sealed class CmoDividendTextTest : GameTest
{
    [Test]
    public async Task DividendTextQuotesItsNumbers()
    {
        var loc = Pair.Server.ResolveDependency<ILocalizationManager>();

        await Pair.Server.WaitAssertion(() =>
        {
            var text = loc.GetString("fs-cmo-dividend-effects");
            Assert.That(text, Does.Contain("$" + FSCmoAbilitySystem.DividendFundCost.ToString("N0", CultureInfo.InvariantCulture)),
                "the Dividend text quotes the wrong cost");
            Assert.That(text, Does.Contain("$" + FSCmoAbilitySystem.DividendPayout.ToString("N0", CultureInfo.InvariantCulture)),
                "the Dividend text quotes the wrong payout");
        });
    }
}
