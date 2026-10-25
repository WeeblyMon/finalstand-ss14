using System.Linq;
using System.Net;
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

    private const string MessageKeyPrefix = "discord_status_message:";
    private static readonly TimeSpan ShutdownWait = TimeSpan.FromSeconds(3);

    private WebhookIdentifier? _webhook;
    private string? _messageKey;
    private ulong? _messageId;
    private string? _lastSent;
    private bool _sending;
    private bool _refreshQueued;
    private TimeSpan _nextUpdate;
    private DateTime? _roundStarted;

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

        if (_webhook is not { } webhook || _messageId is not { } id)
            return;

        try
        {
            var payload = FSDiscordMessages.ServerStatus(BuildReport(offline: true));
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
            return;

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

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.RealTime >= _nextUpdate)
            Refresh();
    }

    private void Refresh()
    {
        if (_webhook is not { } webhook)
            return;

        if (_sending)
        {
            _refreshQueued = true;
            return;
        }

        _refreshQueued = false;
        _nextUpdate = _timing.RealTime + TimeSpan.FromSeconds(Math.Max(10, _cfg.GetCVar(CCVars.FSDiscordStatusIntervalSeconds)));

        var payload = FSDiscordMessages.ServerStatus(BuildReport(offline: false));
        var json = JsonSerializer.Serialize(payload);
        if (json == _lastSent && _messageId != null)
            return;

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
