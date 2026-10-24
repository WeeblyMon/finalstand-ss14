using System.Linq;
using System.Threading.Tasks;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Ladder;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.MedicalOps;
using Content.Server._FinalStand.Titles;
using Content.Server.Discord;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Server;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Discord;

public sealed partial class FSDiscordSystem : EntitySystem
{
    [Dependency] private DiscordWebhook _discord = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IBaseServer _baseServer = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private FSPlayerDataStore _store = default!;
    [Dependency] private FSLevelingSystem _leveling = default!;
    [Dependency] private FSMedicalStatsSystem _medicalStats = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private FSWeeklyChallengeSystem _weekly = default!;

    private const string WeeklyOpenKey = "discord_weekly_open";
    private const string WeeklyLastCallKey = "discord_weekly_lastcall";
    private static readonly TimeSpan WeeklyCheckInterval = TimeSpan.FromMinutes(1);
    private TimeSpan _nextWeeklyCheck;

    private WebhookIdentifier? _webhook;
    private FSRunRecordedEvent? _run;
    private readonly List<(FSRunRecord Run, FSRunRecord? Previous)> _records = new();
    private readonly List<(string Player, string Title)> _rareTitles = new();
    private TimeSpan? _lastLivePing;
    private bool _wasAboveLiveThreshold;
    private readonly HashSet<string> _weeklyPosted = new();

    public WebhookPayload? LastPayload { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.FSDiscordWebhook, url =>
        {
            _webhook = null;
            if (string.IsNullOrWhiteSpace(url))
                return;

            _discord.GetWebhook(url, data =>
            {
                if (_cfg.GetCVar(CCVars.FSDiscordWebhook) == url)
                    _webhook = data.ToIdentifier();
            });
        }, true);

        SubscribeLocalEvent<FSRunRecordedEvent>(OnRunRecorded);
        SubscribeLocalEvent<FSLadderRecordEvent>(OnLadderRecord);
        SubscribeLocalEvent<FSTitleEarnedEvent>(OnTitleEarned);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEndMessage);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ResetRound());
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_webhook == null || _timing.RealTime < _nextWeeklyCheck)
            return;

        _nextWeeklyCheck = _timing.RealTime + WeeklyCheckInterval;
        CheckWeeklyPosts(DateTime.UtcNow);
    }

    public void CheckWeeklyPosts(DateTime utc)
    {
        var challenge = _weekly.GetChallenge(utc);
        if (challenge == null)
            return;

        var week = FSWeeklyRules.WeekKey(utc);
        var lastCall = FSWeeklyRules.IsLastCall(utc);
        var key = lastCall ? WeeklyLastCallKey : WeeklyOpenKey;
        if (!_weeklyPosted.Add($"{key}:{week}") || _store.GetMeta(key) == week)
            return;

        _store.SetMeta(key, week);
        var leader = _store.GetWeeklyTopRuns(week, 1).FirstOrDefault();
        Send(FSDiscordMessages.WeeklyPost(lastCall, Loc.GetString(challenge.Name), Loc.GetString(challenge.Description),
            challenge.ClearWave, leader));
    }

    private void ResetRound()
    {
        _run = null;
        _records.Clear();
        _rareTitles.Clear();
    }

    private void OnRunRecorded(ref FSRunRecordedEvent ev) => _run = ev;

    private void OnLadderRecord(ref FSLadderRecordEvent ev) => _records.Add((ev.Run, ev.Previous));

    private void OnTitleEarned(ref FSTitleEarnedEvent ev)
    {
        if (!_proto.TryIndex(ev.Title, out var title))
            return;
        if (!FSDiscordMessages.IsRareTitle(_store.CountTitleHolders(ev.Title), _store.CountKnownPlayers()))
            return;

        var name = _playerManager.TryGetSessionById(ev.UserId, out var session) ? session.Name : ev.UserId.ToString();
        _rareTitles.Add((name, Loc.GetString(title.Name)));
    }

    private void OnRoundEndMessage(RoundEndMessageEvent ev)
    {
        if (_run is not { } recorded)
            return;

        var report = new FSDiscordMessages.RoundReport(
            _baseServer.ServerName,
            _ticker.RoundId,
            recorded.Run,
            FindMvp(byRevives: false),
            FindMvp(byRevives: true),
            _records.ToList(),
            _rareTitles.ToList(),
            recorded.PreviousHolder);

        Send(FSDiscordMessages.RoundSummary(report));
        ResetRound();
    }

    private FSDiscordMessages.Mvp? FindMvp(bool byRevives)
    {
        FSDiscordMessages.Mvp? best = null;
        var query = EntityQueryEnumerator<FSPlayerLevelComponent, MindComponent>();
        while (query.MoveNext(out var mindId, out _, out var mind))
        {
            var kills = _leveling.GetRoundStats(mindId).Kills;
            var revives = _medicalStats.GetStats(mindId).Revives;
            var candidate = new FSDiscordMessages.Mvp(mind.CharacterName ?? Loc.GetString("fs-leveling-unknown-name"), kills, revives);
            var score = byRevives ? revives : kills;
            var bestScore = best == null ? 0 : byRevives ? best.Value.Revives : best.Value.Kills;
            if (score > bestScore)
                best = candidate;
        }

        return best;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        var threshold = _cfg.GetCVar(CCVars.FSDiscordLivePlayers);
        if (threshold <= 0)
            return;

        var players = _playerManager.Sessions.Count(s => s.Status == SessionStatus.InGame);
        var above = players >= threshold;
        var crossed = above && !_wasAboveLiveThreshold;
        _wasAboveLiveThreshold = above;
        if (!crossed)
            return;

        var cooldown = TimeSpan.FromMinutes(_cfg.GetCVar(CCVars.FSDiscordLiveCooldownMinutes));
        if (_lastLivePing is { } last && _timing.RealTime - last < cooldown)
            return;

        _lastLivePing = _timing.RealTime;
        Send(FSDiscordMessages.ServerLive(_baseServer.ServerName, players, _waveRule.GetWaveNumber()));
    }

    private void Send(WebhookPayload payload)
    {
        LastPayload = payload;
        if (_webhook is not { } webhook)
            return;

        _ = SendAsync(webhook, payload);
    }

    private async Task SendAsync(WebhookIdentifier webhook, WebhookPayload payload)
    {
        try
        {
            var response = await _discord.CreateMessage(webhook, payload);
            if (!response.IsSuccessStatusCode)
                Log.Warning($"Webhook returned {(int) response.StatusCode}");
        }
        catch (Exception e)
        {
            Log.Error($"Failed to post to Discord: {e.Message}");
        }
    }
}
