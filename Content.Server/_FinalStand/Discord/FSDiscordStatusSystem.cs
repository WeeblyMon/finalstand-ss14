using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.Discord;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Server;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Discord;

public sealed partial class FSDiscordStatusSystem : EntitySystem
{
    [Dependency] private DiscordWebhook _discord = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IBaseServer _baseServer = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private FSPlayerDataStore _store = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private ITaskManager _tasks = default!;

    private const string MessageKeyPrefix = "discord_status_message:";
    private static readonly TimeSpan ShutdownWait = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan TimerPeriod = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Heartbeat = TimeSpan.FromMinutes(5);

    private WebhookIdentifier? _webhook;
    private string? _messageKey;
    private ulong? _messageId;
    private string? _lastSent;
    private bool _sending;
    private bool _refreshQueued;
    private TimeSpan _nextUpdate;
    private DateTime? _roundStarted;
    private DateTime _lastPush;
    private int _offlineSent;
    private System.Threading.Timer? _timer;
    private readonly List<PosixSignalRegistration> _signals = new();

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(CCVars.FSDiscordStatusWebhook, OnWebhookChanged, true);

        SubscribeLocalEvent<RoundStartingEvent>(_ => _roundStarted = DateTime.UtcNow);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _roundStarted = null);

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _cfg.UnsubValueChanged(CCVars.FSDiscordStatusWebhook, OnWebhookChanged);
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        ReleaseProcessHooks();
        MarkOffline();
    }

    public bool HoldsProcessHooks => _timer != null;

    // The timer, exit hook and signal handlers reach this system from outside the server, and a stopping server only
    // clears its systems without calling Shutdown, so they would keep the whole server alive (every pair a test run
    // throws away). They exist only while there is a webhook to report to.
    private void HoldProcessHooks()
    {
        if (_timer != null)
            return;

        _timer = new System.Threading.Timer(_ => _tasks.RunOnMainThread(OnTimer), null, TimerPeriod, TimerPeriod);

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        foreach (var signal in new[] { PosixSignal.SIGHUP, PosixSignal.SIGTERM })
        {
            try
            {
                _signals.Add(PosixSignalRegistration.Create(signal, _ => MarkOffline()));
            }
            catch (PlatformNotSupportedException)
            {
            }
        }
    }

    private void ReleaseProcessHooks()
    {
        if (_timer == null)
            return;

        _timer.Dispose();
        _timer = null;

        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        foreach (var signal in _signals)
            signal.Dispose();
        _signals.Clear();
    }

    private void OnProcessExit(object? sender, EventArgs e) => MarkOffline();

    private void MarkOffline()
    {
        if (_webhook is not { } webhook || _messageId is not { } id)
            return;

        if (Interlocked.Exchange(ref _offlineSent, 1) == 1)
            return;

        try
        {
            var payload = FSDiscordMessages.ServerStatus(BuildReport(offline: true) with { Updated = DateTime.UtcNow });
            Task.Run(() => _discord.EditMessage(webhook, id, payload)).Wait(ShutdownWait);
        }
        catch (Exception e)
        {
            Log.Warning($"[FSDiscordStatus] Could not mark the server offline: {e.Message}");
        }
    }

    private void OnWebhookChanged(string url)
    {
        _webhook = null;
        _messageKey = null;
        _messageId = null;
        _lastSent = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            ReleaseProcessHooks();
            return;
        }

        HoldProcessHooks();
        _discord.GetWebhook(url, data =>
        {
            if (_cfg.GetCVar(CCVars.FSDiscordStatusWebhook) != url)
                return;

            _webhook = data.ToIdentifier();
            _messageKey = MessageKeyPrefix + data.Id;
            _messageId = ulong.TryParse(_store.GetMeta(_messageKey), out var stored) ? stored : null;
            _nextUpdate = TimeSpan.Zero;
        });
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus is SessionStatus.Connected or SessionStatus.Disconnected)
            Refresh();
    }

    private void OnTimer()
    {
        if (_offlineSent == 1)
            return;

        if (DateTime.UtcNow - _lastPush >= Heartbeat)
            Refresh(force: true);
        else if (_timing.RealTime >= _nextUpdate)
            Refresh();
    }

    private void Refresh(bool force = false)
    {
        if (_webhook is not { } webhook)
            return;

        if (_offlineSent == 1)
            return;

        if (_sending)
        {
            _refreshQueued = true;
            return;
        }

        _refreshQueued = false;
        _nextUpdate = _timing.RealTime + TimeSpan.FromSeconds(Math.Max(10, _cfg.GetCVar(CCVars.FSDiscordStatusIntervalSeconds)));

        var report = BuildReport(offline: false);
        var json = JsonSerializer.Serialize(FSDiscordMessages.ServerStatus(report));
        if (json == _lastSent && _messageId != null && !force)
            return;

        var payload = FSDiscordMessages.ServerStatus(report with { Updated = DateTime.UtcNow });
        _lastPush = DateTime.UtcNow;

        _sending = true;
        _ = PushAsync(webhook, payload, json);
    }

    public FSDiscordMessages.StatusReport BuildReport(bool offline)
    {
        var phase = FSDiscordMessages.StatusPhase.Offline;
        var wave = 0;

        if (!offline)
        {
            phase = _ticker.RunLevel switch
            {
                GameRunLevel.PreRoundLobby => FSDiscordMessages.StatusPhase.Lobby,
                GameRunLevel.PostRound => FSDiscordMessages.StatusPhase.RoundOver,
                _ => FSDiscordMessages.StatusPhase.InRound,
            };

            if (phase == FSDiscordMessages.StatusPhase.InRound && _waveRule.TryGetActiveState(out var state))
            {
                wave = state.WaveNumber;
                phase = state.Phase == WavePhase.Combat
                    ? FSDiscordMessages.StatusPhase.Combat
                    : FSDiscordMessages.StatusPhase.Prep;
            }
        }

        var inRound = phase is FSDiscordMessages.StatusPhase.InRound or FSDiscordMessages.StatusPhase.Prep or FSDiscordMessages.StatusPhase.Combat;

        return new FSDiscordMessages.StatusReport(
            _baseServer.ServerName,
            phase,
            offline ? 0 : _playerManager.Sessions.Count(s => s.Status != SessionStatus.Disconnected),
            _cfg.GetCVar(CVars.GameMaxPlayers),
            wave,
            inRound && _waveRule.IsEndless(),
            inRound ? _waveRule.GetDifficulty() : null,
            inRound ? _waveRule.GetActiveModifiers().Select(m => Loc.GetString(m.Name)).ToList() : [],
            inRound ? _roundStarted : null,
            _cfg.GetCVar(CCVars.FSDiscordStatusConnect));
    }

    private async Task PushAsync(WebhookIdentifier webhook, WebhookPayload payload, string json)
    {
        try
        {
            if (_messageId is { } id)
            {
                var edit = await _discord.EditMessage(webhook, id, payload);
                if (edit.IsSuccessStatusCode)
                {
                    _lastSent = json;
                    return;
                }

                if (edit.StatusCode != HttpStatusCode.NotFound)
                    return;

                _messageId = null;
            }

            var create = await _discord.CreateMessage(webhook, payload);
            if (!create.IsSuccessStatusCode)
                return;

            var content = await create.Content.ReadAsStringAsync();
            if (!ulong.TryParse(JsonNode.Parse(content)?["id"]?.GetValue<string>(), out var created))
                return;

            _messageId = created;
            _lastSent = json;
            if (_messageKey != null)
                _store.SetMeta(_messageKey, created.ToString());
        }
        catch (Exception e)
        {
            Log.Warning($"[FSDiscordStatus] Failed to update the status message: {e.Message}");
        }
        finally
        {
            _sending = false;
            if (_refreshQueued)
                Refresh();
        }
    }
}
