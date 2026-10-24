using System.Linq;
using Content.Server.Voting;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.CCVar;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.GameTicking.Rules;

public sealed partial class WaveGameRuleSystem
{
    private IVoteHandle? _endlessVote;

    private void OfferEndless(WaveGameRuleComponent comp)
    {
        var seconds = _cfg.GetCVar(CCVars.FSEndlessVoteSeconds);
        if (seconds <= 0)
        {
            _roundEnd.EndRound(VictoryRoundEndCountdown);
            return;
        }

        comp.AwaitingEndless = true;
        var options = new VoteOptions
        {
            Title = Loc.GetString("fs-endless-vote-title"),
            InitiatorText = Loc.GetString("fs-difficulty-vote-initiator"),
            Duration = TimeSpan.FromSeconds(seconds),
            Options =
            {
                (Loc.GetString("fs-endless-vote-yes"), true),
                (Loc.GetString("fs-endless-vote-no"), false),
            },
        };

        _endlessVote = _votes.CreateVote(options);
        _endlessVote.OnFinished += OnEndlessVoteFinished;
        _endlessVote.OnCancelled += OnEndlessVoteCancelled;
    }

    private void OnEndlessVoteCancelled(IVoteHandle sender)
    {
        if (sender != _endlessVote)
            return;

        _endlessVote = null;
        TryDeclineEndless();
    }

    private void OnEndlessVoteFinished(IVoteHandle sender, VoteFinishedEventArgs args)
    {
        if (sender != _endlessVote)
            return;
        _endlessVote = null;

        if (args.Winner is true)
            TryStartEndless();
        else
            TryDeclineEndless();
    }

    public bool IsAwaitingEndless() => TryGetActiveRule(out _, out var comp) && comp.AwaitingEndless;

    public bool IsEndless() => TryGetActiveRule(out _, out var comp) && comp.Endless;

    public bool TryStartEndless()
    {
        if (!TryGetActiveRule(out var uid, out var comp) || !comp.AwaitingEndless)
            return false;

        CancelEndlessVote();
        comp.AwaitingEndless = false;
        comp.Endless = true;
        Log.Info($"The crew pushes on past wave {comp.WaveNumber} into endless.");
        _chatManager.DispatchServerAnnouncement(
            Loc.GetString("fs-endless-started", ("interval", comp.EndlessEscalationInterval)), AnnounceColor);

        comp.WaveNumber++;
        EscalateEndless(comp);
        StartPrepPhase(uid, comp);
        return true;
    }

    public bool TryDeclineEndless()
    {
        if (!TryGetActiveRule(out _, out var comp) || !comp.AwaitingEndless)
            return false;

        CancelEndlessVote();
        comp.AwaitingEndless = false;
        _roundEnd.EndRound(VictoryRoundEndCountdown);
        return true;
    }

    public static bool IsEscalationWave(int wave, int victoryWave, int interval)
        => interval > 0 && wave > victoryWave && (wave - victoryWave) % interval == 0;

    private void EscalateEndless(WaveGameRuleComponent comp)
    {
        if (!IsEscalationWave(comp.WaveNumber, comp.VictoryWave, comp.EndlessEscalationInterval))
            return;

        var active = GetModifiers(comp).ToList();
        var pool = _proto.EnumeratePrototypes<FSRoundModifierPrototype>()
            .Where(m => active.All(a => a.ID != m.ID && FSRoundModifierRules.Compatible(a, m)))
            .OrderBy(m => m.ID)
            .ToList();

        var picked = FSRoundModifierRules.Roll(pool, 1, () => RobustRandom.NextFloat()).FirstOrDefault();
        if (picked != null)
        {
            comp.Modifiers.Add(new ProtoId<FSRoundModifierPrototype>(picked.ID));
            _chatManager.DispatchServerAnnouncement(Loc.GetString("fs-endless-escalation",
                ("wave", comp.WaveNumber),
                ("name", Loc.GetString(picked.Name)),
                ("description", Loc.GetString(picked.Description))), AnnounceColor);
        }
        else
        {
            _chatManager.DispatchServerAnnouncement(Loc.GetString("fs-endless-escalation-max", ("wave", comp.WaveNumber)),
                AnnounceColor);
        }
    }

    private void CancelEndlessVote()
    {
        if (_endlessVote == null)
            return;

        var vote = _endlessVote;
        _endlessVote = null;
        vote.OnFinished -= OnEndlessVoteFinished;
        vote.OnCancelled -= OnEndlessVoteCancelled;
        vote.Cancel();
    }
}
