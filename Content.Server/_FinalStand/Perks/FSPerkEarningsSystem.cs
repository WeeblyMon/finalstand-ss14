using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Perks;

public sealed class FSPerkEarningsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded, after: [typeof(FSInvestorSystem)]);
    }

    public void RecordCredits(EntityUid mindId, string perkId, int amount)
    {
        if (amount <= 0)
            return;

        var totals = Totals(mindId, perkId);
        totals.WaveCredits += amount;
        totals.GameCredits += amount;
    }

    public void RecordResearch(EntityUid mindId, string perkId, int amount)
    {
        if (amount <= 0)
            return;

        var totals = Totals(mindId, perkId);
        totals.WaveResearch += amount;
        totals.GameResearch += amount;
    }

    private FSPerkEarningsTotals Totals(EntityUid mindId, string perkId)
    {
        var comp = EnsureComp<FSPerkEarningsComponent>(mindId);
        if (!comp.Entries.TryGetValue(perkId, out var totals))
            comp.Entries[perkId] = totals = new FSPerkEarningsTotals();
        return totals;
    }

    private void OnWaveEnded(ref WaveEndedEvent ev)
    {
        var query = EntityQueryEnumerator<FSPerkEarningsComponent, MindComponent>();
        while (query.MoveNext(out _, out var earnings, out var mind))
        {
            var summary = new FSPerkEarningsSummaryEvent();

            foreach (var (perkId, totals) in earnings.Entries)
            {
                if (totals.WaveCredits <= 0 && totals.WaveResearch <= 0)
                    continue;

                summary.Entries.Add(new FSPerkEarningsEntry
                {
                    PerkId = perkId,
                    WaveCredits = totals.WaveCredits,
                    GameCredits = totals.GameCredits,
                    WaveResearch = totals.WaveResearch,
                    GameResearch = totals.GameResearch,
                });

                totals.WaveCredits = 0;
                totals.WaveResearch = 0;
            }

            if (summary.Entries.Count == 0
                || mind.CurrentEntity is not { } body
                || !TryComp<ActorComponent>(body, out var actor))
                continue;

            summary.Entries.Sort(static (a, b) => string.CompareOrdinal(a.PerkId, b.PerkId));
            RaiseNetworkEvent(summary, actor.PlayerSession);
        }
    }
}
