using System.Linq;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Onboarding;
using Content.Server._FinalStand.Titles;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Ladder;

public sealed partial class FSWeeklyChallengeSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IVoteManager _votes = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private FSPlayerDataStore _store = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSRoundPresenceSystem _presence = default!;
    [Dependency] private FSTitleSystem _titles = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    public const string ChampionTitle = "FSTitleWeeklyChampion";
    private static readonly TimeSpan VoteDuration = TimeSpan.FromSeconds(30);

    private IVoteHandle? _vote;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
        SubscribeLocalEvent<FSRunRecordedEvent>(OnRunRecorded);
    }

    public FSWeeklyChallengePrototype? GetChallenge(DateTime utc)
    {
        var rotation = _proto.EnumeratePrototypes<FSWeeklyChallengePrototype>().OrderBy(c => c.Order).ThenBy(c => c.ID).ToList();
        return FSWeeklyRules.Pick(rotation, utc);
    }

    public bool TryStartVote(ICommonSession? initiator, out string error)
    {
        error = string.Empty;
        var challenge = GetChallenge(DateTime.UtcNow);
        if (challenge == null)
        {
            error = Loc.GetString("fs-weekly-none");
            return false;
        }

        if (!_waveRule.TryGetWaveStatus(out _, out _) || _waveRule.IsLocked() || _waveRule.TryGetWeekly(out _, out _))
        {
            error = Loc.GetString("fs-weekly-too-late");
            return false;
        }

        if (_vote != null)
        {
            error = Loc.GetString("fs-weekly-vote-running");
            return false;
        }

        var options = new VoteOptions
        {
            Title = Loc.GetString("fs-weekly-vote-title", ("name", Loc.GetString(challenge.Name))),
            Duration = VoteDuration,
            Options =
            {
                (Loc.GetString("fs-weekly-vote-yes"), true),
                (Loc.GetString("fs-weekly-vote-no"), false),
            },
        };
        options.SetInitiatorOrServer(initiator);

        _vote = _votes.CreateVote(options);
        _vote.OnFinished += (sender, args) =>
        {
            if (sender != _vote)
                return;
            _vote = null;
            if (args.Winner is true)
                StartWeekly(challenge);
        };
        _vote.OnCancelled += sender =>
        {
            if (sender == _vote)
                _vote = null;
        };
        return true;
    }

    public bool StartWeekly(FSWeeklyChallengePrototype challenge)
        => _waveRule.TryStartWeekly(challenge, FSWeeklyRules.WeekKey(DateTime.UtcNow));

    private void OnRoundEndText(RoundEndTextAppendEvent args)
    {
        if (!_waveRule.TryGetWeekly(out var challenge, out var week) || !_waveRule.TryGetRoundSummary(out var summary))
            return;

        var cleared = summary.Victory || summary.WavesCleared >= challenge.ClearWave;
        args.AddLine(Loc.GetString(cleared ? "fs-weekly-round-end-cleared" : "fs-weekly-round-end-failed",
            ("name", Loc.GetString(challenge.Name)), ("wave", challenge.ClearWave)));

        if (!cleared)
            return;

        foreach (var session in _playerManager.Sessions)
        {
            if (_presence.GetWavesPresent(session.UserId) <= 0 || !_mind.TryGetMind(session, out var mindId, out _))
                continue;
            if (!_store.TryClaimWeeklyReward(session.UserId.UserId, week))
                continue;

            _wallet.AddPerkPoints(mindId, FSWeeklyRules.ClearPerkPoints);
            _chat.DispatchServerMessage(session, Loc.GetString("fs-weekly-reward", ("points", FSWeeklyRules.ClearPerkPoints)));
        }
    }

    private void OnRunRecorded(ref FSRunRecordedEvent ev)
    {
        if (ev.Run.Weekly == null || !ev.IsRecord)
            return;

        foreach (var session in _playerManager.Sessions)
        {
            if (_presence.GetWavesPresent(session.UserId) > 0)
                _titles.Grant(session.UserId, ChampionTitle);
        }
    }
}
