using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Economy;
using Content.Shared.Mind;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSInvestorSystem : EntitySystem
{
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private FSPerkEarningsSystem _earnings = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
    }

    private void OnWaveEnded(ref WaveEndedEvent ev)
    {
        var highestFundLevel = 0;
        var fundQuery = EntityQueryEnumerator<FSPerkLevelsComponent>();
        while (fundQuery.MoveNext(out _, out var a))
        {
            var fl = a.GetSlottedLevel("MutualFund");
            if (fl > highestFundLevel) highestFundLevel = fl;
        }

        var query = EntityQueryEnumerator<FSPerkLevelsComponent, FSPlayerWalletComponent, MindComponent>();
        while (query.MoveNext(out var mindId, out var augs, out var wallet, out _))
        {
            var investorLevel = augs.GetSlottedLevel("Investor");
            var personalRate = investorLevel * FSPerkBonusConstants.InvestorPerLevel;
            var fundRate = highestFundLevel * FSPerkBonusConstants.MutualFundPerLevel;
            if (personalRate + fundRate <= 0f) continue;

            var investorAmount = (int)(wallet.Credits * personalRate);
            var fundAmount = (int)(wallet.Credits * fundRate);
            var total = investorAmount + fundAmount;
            if (total <= 0) continue;

            _wallet.GiveCredits(mindId, total);
            _earnings.RecordCredits(mindId, "Investor", investorAmount);
            _earnings.RecordCredits(mindId, "MutualFund", fundAmount);
        }
    }
}
