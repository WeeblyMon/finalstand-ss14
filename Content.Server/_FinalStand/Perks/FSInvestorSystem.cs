using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Economy;
using Content.Shared.Mind;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSInvestorSystem : EntitySystem
{
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private SharedMindSystem _mind = default!;

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
        while (query.MoveNext(out var mindId, out var augs, out var wallet, out var mind))
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

            if (!mind.CurrentEntity.HasValue) continue;
            if (!TryComp<ActorComponent>(mind.CurrentEntity.Value, out var actor)) continue;

            if (investorAmount > 0)
                RaiseNetworkEvent(new FSInterestPayoutEvent { PerkId = "Investor", Amount = investorAmount },
                    actor.PlayerSession);
            if (fundAmount > 0)
                RaiseNetworkEvent(new FSInterestPayoutEvent { PerkId = "MutualFund", Amount = fundAmount },
                    actor.PlayerSession);
        }
    }
}
