using Content.Server._FinalStand.Augments;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.Economy;

namespace Content.Server._FinalStand.Augments;

public sealed class FSInvestorSystem : EntitySystem
{
    [Dependency] private readonly FSPlayerWalletSystem _wallet = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
    }

    private void OnWaveEnded(ref WaveEndedEvent ev)
    {
        // Find the highest MutualFund level slotted by any player.
        var highestFundLevel = 0;
        var fundQuery = EntityQueryEnumerator<FSAugmentLevelsComponent>();
        while (fundQuery.MoveNext(out _, out var a))
        {
            var fl = a.GetSlottedLevel("MutualFund");
            if (fl > highestFundLevel) highestFundLevel = fl;
        }

        var query = EntityQueryEnumerator<FSAugmentLevelsComponent, FSPlayerWalletComponent>();
        while (query.MoveNext(out var mindId, out var augs, out var wallet))
        {
            var investorLevel = augs.GetSlottedLevel("Investor");
            var personalRate = investorLevel * 0.025f;
            var fundRate = highestFundLevel * 0.0125f;
            var totalRate = personalRate + fundRate;
            if (totalRate <= 0f) continue;

            var interest = (int)(wallet.Credits * totalRate);
            if (interest <= 0) continue;
            _wallet.GiveCredits(mindId, interest);
        }
    }
}
