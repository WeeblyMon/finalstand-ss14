using System.Linq;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Onboarding;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Ladder;

[ByRefEvent]
public readonly record struct FSLadderRecordEvent(FSRunRecord Run, FSRunRecord? Previous);

[ByRefEvent]
public readonly record struct FSRunRecordedEvent(FSRunRecord Run, FSRunRecord? PreviousHolder, bool IsRecord);

public sealed partial class FSLadderSystem : EntitySystem
{
    [Dependency] private FSPlayerDataStore _store = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private FSRoundPresenceSystem _presence = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FSWeeklyChallengeSystem _weekly = default!;

    private const int MaxPlayersText = 200;
    private static readonly TimeSpan RequestInterval = TimeSpan.FromSeconds(0.5);

    private readonly Dictionary<NetUserId, TimeSpan> _lastRequest = new();
    private bool _recordedThisRound;
    private long? _provisionalRun;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _recordedThisRound = false;
            _provisionalRun = null;
        });
        SubscribeLocalEvent<FSRoundVictoryEvent>((ref FSRoundVictoryEvent _) => SaveProvisionalRun());
        SubscribeLocalEvent<WavePrepStartedEvent>(_ =>
        {
            if (_provisionalRun != null)
                SaveProvisionalRun();
        });
        SubscribeNetworkEvent<FSLadderRequestEvent>(OnLadderRequest);
        SubscribeLocalEvent<FSLadderBoardComponent, ActivateInWorldEvent>(OnBoardActivated);
        SubscribeLocalEvent<FSLadderBoardComponent, ExaminedEvent>(OnBoardExamined);
        SubscribeLocalEvent<FSLadderBoardComponent, MapInitEvent>((uid, board, _) => RefreshBoard((uid, board)));
        SubscribeLocalEvent<FSRunRecordedEvent>((ref FSRunRecordedEvent _) => RefreshBoards());
        SubscribeLocalEvent<RoundStartingEvent>(_ => RefreshBoards());
    }

    public void RefreshBoards()
    {
        var query = EntityQueryEnumerator<FSLadderBoardComponent>();
        while (query.MoveNext(out var uid, out var board))
            RefreshBoard((uid, board));
    }

    private void RefreshBoard(Entity<FSLadderBoardComponent> board)
    {
        string? weeklyName = null;
        List<FSRunRecord> runs;
        if (board.Comp.Weekly)
        {
            var utcNow = DateTime.UtcNow;
            weeklyName = _weekly.GetChallenge(utcNow) is { } challenge ? Loc.GetString(challenge.Name) : null;
            runs = _store.GetWeeklyTopRuns(FSWeeklyRules.WeekKey(utcNow), FSLadderBoardComponent.ShownRuns);
        }
        else
        {
            runs = _store.GetTopRuns(board.Comp.Difficulty, board.Comp.AllBrackets ? null : board.Comp.Bracket,
                FSLadderBoardComponent.ShownRuns);
        }

        if (weeklyName == board.Comp.WeeklyName && runs.SequenceEqual(board.Comp.Runs))
            return;

        board.Comp.WeeklyName = weeklyName;
        board.Comp.Runs = runs;
        Dirty(board);
    }

    public List<FSRunRecord> GetTopRuns(FSDifficulty difficulty, FSPlayerBracket bracket, int limit = FSLadder.PageSize)
        => _store.GetTopRuns(difficulty, bracket, limit);

    private void OnRoundEndText(RoundEndTextAppendEvent args)
    {
        if (_recordedThisRound || _ticker.RunLevel != GameRunLevel.PostRound
            || !_waveRule.TryGetRoundSummary(out var summary) || summary.WavesCleared <= 0)
            return;

        _recordedThisRound = true;
        if (_provisionalRun is { } provisional)
        {
            _store.DeleteRun(provisional);
            _provisionalRun = null;
        }

        if (summary.Assisted)
        {
            args.AddLine(Loc.GetString("fs-ladder-assisted"));
            return;
        }

        if (BuildCurrentRun(summary) is not { } run || !RecordRun(run))
            return;

        args.AddLine(run.Weekly != null
            ? Loc.GetString("fs-ladder-new-weekly-record")
            : Loc.GetString("fs-ladder-new-record",
                ("difficulty", Loc.GetString(FSDifficultyRules.NameLocId(run.Difficulty))),
                ("bracket", Loc.GetString(FSLadder.BracketLocId(run.Bracket)))));
    }

    private void SaveProvisionalRun()
    {
        if (_recordedThisRound || !_waveRule.TryGetRoundSummary(out var summary) || summary.Assisted
            || !summary.Victory || BuildCurrentRun(summary) is not { } run)
            return;

        if (_provisionalRun is { } id)
            _store.UpdateRun(id, run);
        else
            _provisionalRun = _store.InsertRun(run);
        RefreshBoards();
    }

    private FSRunRecord? BuildCurrentRun(WaveGameRuleSystem.RoundSummary summary)
    {
        var names = new List<string>();
        var seen = new HashSet<NetUserId>();
        var query = EntityQueryEnumerator<FSPlayerLevelComponent, MindComponent>();
        while (query.MoveNext(out _, out _, out var mind))
        {
            if (mind.UserId is not { } userId || _presence.GetWavesPresent(userId) <= 0 || !seen.Add(userId))
                continue;
            names.Add(mind.CharacterName ?? "Unknown");
        }

        return names.Count == 0
            ? null
            : BuildRun(names, summary, (int) _ticker.RoundDuration().TotalSeconds, DateTime.UtcNow);
    }

    public static FSRunRecord BuildRun(IReadOnlyCollection<string> names, WaveGameRuleSystem.RoundSummary summary,
        int durationSeconds, DateTime endedAtUtc)
    {
        var players = string.Join(", ", names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
        if (players.Length > MaxPlayersText)
            players = players[..(MaxPlayersText - 1)] + "…";

        return new FSRunRecord(players, summary.Difficulty, FSLadder.BracketFor(names.Count), summary.WavesCleared,
            summary.Victory, Math.Max(0, durationSeconds), summary.Kills, endedAtUtc, summary.WeeklyKey);
    }

    public bool RecordRun(FSRunRecord run)
    {
        var holder = run.Weekly is { } week
            ? _store.GetWeeklyTopRuns(week, 1).FirstOrDefault()
            : _store.GetTopRuns(run.Difficulty, run.Bracket, 1).FirstOrDefault();
        _store.InsertRun(run);

        var isRecord = FSLadder.Beats(run, holder);
        if (isRecord)
        {
            var record = new FSLadderRecordEvent(run, holder);
            RaiseLocalEvent(ref record);
        }

        var recorded = new FSRunRecordedEvent(run, holder, isRecord);
        RaiseLocalEvent(ref recorded);
        return isRecord;
    }

    private void OnLadderRequest(FSLadderRequestEvent msg, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.UserId;
        var now = _timing.RealTime;
        if (_lastRequest.TryGetValue(user, out var last) && now - last < RequestInterval)
            return;
        _lastRequest[user] = now;

        if (!Enum.IsDefined(msg.Difficulty) || !Enum.IsDefined(msg.Bracket))
            return;

        if (msg.Weekly)
        {
            var utcNow = DateTime.UtcNow;
            var challenge = _weekly.GetChallenge(utcNow);
            RaiseNetworkEvent(new FSLadderResponseEvent
            {
                Difficulty = msg.Difficulty,
                Bracket = msg.Bracket,
                Weekly = true,
                WeeklyName = challenge == null ? null : Loc.GetString(challenge.Name),
                Runs = _store.GetWeeklyTopRuns(FSWeeklyRules.WeekKey(utcNow), FSLadder.PageSize).ToArray(),
            }, args.SenderSession);
            return;
        }

        RaiseNetworkEvent(new FSLadderResponseEvent
        {
            Difficulty = msg.Difficulty,
            Bracket = msg.Bracket,
            Runs = GetTopRuns(msg.Difficulty, msg.Bracket).ToArray(),
        }, args.SenderSession);
    }

    private void OnBoardActivated(Entity<FSLadderBoardComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || !TryComp(args.User, out ActorComponent? actor))
            return;

        RaiseNetworkEvent(new FSOpenLadderEvent(), actor.PlayerSession);
        args.Handled = true;
    }

    private void OnBoardExamined(Entity<FSLadderBoardComponent> ent, ref ExaminedEvent args)
    {
        var ladder = ent.Comp.Weekly
            ? Loc.GetString("fs-ladder-weekly")
            : Loc.GetString("fs-ladder-board-ladder",
                ("difficulty", Loc.GetString(FSDifficultyRules.NameLocId(ent.Comp.Difficulty))),
                ("bracket", Loc.GetString(ent.Comp.AllBrackets ? "fs-ladder-bracket-all" : FSLadder.BracketLocId(ent.Comp.Bracket))));

        args.PushMarkup(ent.Comp.Runs.FirstOrDefault() is not { } best
            ? Loc.GetString("fs-ladder-board-examine-empty", ("ladder", ladder))
            : Loc.GetString("fs-ladder-board-examine", ("ladder", ladder), ("players", best.Players), ("waves", best.Waves)));
    }
}
