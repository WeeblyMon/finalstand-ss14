using Content.Shared._FinalStand.Leveling;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.ReadyCheck;
using Content.Shared._FinalStand.Respawn;
using Content.Shared._FinalStand.WaveHud;
using Robust.Client;
using Robust.Client.Graphics;
using Content.Client._FinalStand.MedicalOps;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IBaseClient _client = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    private WaveHudOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<WaveCounterUpdateEvent>(OnWaveUpdate);
        SubscribeNetworkEvent<WalletUpdatedEvent>(OnWalletUpdate);
        SubscribeNetworkEvent<FSEnemyCountEvent>(OnEnemyCount);
        SubscribeNetworkEvent<FSPerksStateEvent>(OnPerksState);
        SubscribeNetworkEvent<FSPrepTimerUpdateEvent>(OnPrepTimer);
        SubscribeNetworkEvent<WavePhaseChangedEvent>(OnPhaseChanged);
        SubscribeNetworkEvent<FSReadyUpStateEvent>(OnReadyUpState);
        SubscribeNetworkEvent<FSPerkStacksUpdateEvent>(OnPerkStacksUpdate);
        SubscribeNetworkEvent<FSInterestPayoutEvent>(OnInterestPayout);
        SubscribeNetworkEvent<FSPlayerBonusSummaryEvent>(OnBonusSummary);
        SubscribeNetworkEvent<FSDarkWaveStartedEvent>(OnDarkWaveStarted);
        SubscribeNetworkEvent<FSDarkWaveEndedEvent>(OnDarkWaveEnded);
        SubscribeNetworkEvent<FSRespawnOfferEvent>(OnRespawnOffer);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
        _client.PlayerJoinedServer += OnPlayerJoinedServer;
    }

    private void OnDarkWaveStarted(FSDarkWaveStartedEvent ev)
    {
        var overlay = EnsureOverlay();
        overlay.IsDarkWave = true;
        overlay.DarkWaveSecondsRemaining = ev.DurationSeconds;
    }

    private void OnDarkWaveEnded(FSDarkWaveEndedEvent ev)
    {
        var overlay = EnsureOverlay();
        overlay.IsDarkWave = false;
        overlay.DarkWaveSecondsRemaining = 0f;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_overlay is not { } overlay)
            return;

        UpdateCasualtyStatus(overlay);
        UpdateBuffStatus(overlay);
        UpdateHarvestStatus(overlay);

        if (!overlay.IsDarkWave)
            return;

        overlay.DarkWaveSecondsRemaining = Math.Max(0f, overlay.DarkWaveSecondsRemaining - frameTime);
    }

    private void UpdateHarvestStatus(WaveHudOverlay overlay)
    {
        overlay.HarvestStatus = null;
        overlay.HarvestCapped = false;

        if (_player.LocalEntity is not { } player)
            return;

        var query = EntityQueryEnumerator<FSHarvestSatchelComponent>();
        while (query.MoveNext(out var uid, out var satchel))
        {
            if (!IsCarriedBy(uid, player))
                continue;

            var stock = 0f;
            if (_solutions.TryGetSolution(uid, satchel.Solution, out _, out var solution))
                stock = solution.GetTotalPrototypeQuantity(satchel.Reagent).Float();

            overlay.HarvestCapped = satchel.AccruedThisWave >= satchel.PerWaveCap;

            overlay.HarvestStatus = overlay.HarvestCapped
                ? Loc.GetString("fs-harvest-status-capped", ("stock", (int) stock))
                : Loc.GetString("fs-harvest-status",
                    ("stock", (int) stock),
                    ("wave", (int) satchel.AccruedThisWave),
                    ("cap", (int) satchel.PerWaveCap));

            return;
        }
    }

    private bool IsCarriedBy(EntityUid item, EntityUid carrier)
    {
        var parent = Transform(item).ParentUid;

        for (var depth = 0; depth < 5 && parent.IsValid(); depth++)
        {
            if (parent == carrier)
                return true;

            parent = Transform(parent).ParentUid;
        }

        return false;
    }

    private void UpdateBuffStatus(WaveHudOverlay overlay)
    {
        overlay.MedicalBuffs.Clear();

        if (_player.LocalEntity is not { } player
            || !TryComp<FSMedicalBonusComponent>(player, out var bonus))
        {
            return;
        }

        var now = _timing.CurTime;

        foreach (var (source, buff) in bonus.Active)
        {
            if (!source.StartsWith(FSHealthBarSystem.ChemSourcePrefix) || buff.IsExpired(now))
                continue;

            var label = buff.Name ?? Loc.GetString("fs-buff-status");

            var seconds = buff.EndTime is { } end
                ? (int) Math.Ceiling((end - now).TotalSeconds)
                : -1;

            overlay.MedicalBuffs.Add(new WaveHudOverlay.MedicalBuffRow(
                label,
                BuffIconKey(source),
                seconds,
                Loc.GetString("fs-buff-source-chem")));
        }
    }

    // Icon keys resolve to /Textures/_FinalStand/Interface/HUD/hud_stat_{key}.png and fall back when absent.
    private static string BuffIconKey(string source)
    {
        var name = source[FSHealthBarSystem.ChemSourcePrefix.Length..];
        return string.IsNullOrEmpty(name) ? "buff" : "buff_" + name.ToLowerInvariant();
    }

    private void UpdateCasualtyStatus(WaveHudOverlay overlay)
    {
        if (!overlay.IsRespawnOfferVisible
            || _player.LocalEntity is not { } player
            || !TryComp<FSCasualtyStatusComponent>(player, out var status))
        {
            overlay.CasualtyStatus = null;
            overlay.CasualtyResponded = false;
            return;
        }

        if (status.Responder is { } responder)
        {
            overlay.CasualtyStatus = Loc.GetString("fs-casualty-downed-en-route", ("medic", responder));
            overlay.CasualtyResponded = true;
        }
        else
        {
            overlay.CasualtyStatus = Loc.GetString("fs-casualty-downed-waiting");
            overlay.CasualtyResponded = false;
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _client.PlayerJoinedServer -= OnPlayerJoinedServer;

        if (_overlay != null)
        {
            _overlay.OnReadyUpClicked -= SendReadyRequest;
            _overlay.OnRespawnClicked -= SendRespawnRequest;
            _overlayManager.RemoveOverlay(_overlay);
            _overlay = null;
        }
    }

    private void OnPlayerJoinedServer(object? sender, PlayerEventArgs _)
    {
        RaiseNetworkEvent(new WalletRequestEvent());
        RaiseNetworkEvent(new FSPerkStateRequestMessage());
    }

    private void OnLocalPlayerAttached(LocalPlayerAttachedEvent _)
    {
        RaiseNetworkEvent(new FSPerkStateRequestMessage());
    }

    private WaveHudOverlay EnsureOverlay()
    {
        if (_overlay != null)
            return _overlay;
        _overlay = new WaveHudOverlay();
        _overlay.OnReadyUpClicked += SendReadyRequest;
        _overlay.OnRespawnClicked += SendRespawnRequest;
        _overlayManager.AddOverlay(_overlay);
        return _overlay;
    }

    private void SendReadyRequest(bool isReady)
    {
        RaiseNetworkEvent(new FSReadyUpRequestMessage(isReady));
    }

    private void SendRespawnRequest()
    {
        RaiseNetworkEvent(new FSRespawnRequestMessage());
    }

    private void OnRespawnOffer(FSRespawnOfferEvent ev)
    {
        var overlay = EnsureOverlay();
        overlay.IsRespawnOfferVisible = ev.Available;
        overlay.RespawnCost = ev.Cost;
    }

    private void OnWaveUpdate(WaveCounterUpdateEvent ev)
    {
        EnsureOverlay().CurrentWave = ev.Wave;
    }

    private void OnWalletUpdate(WalletUpdatedEvent ev)
    {
        EnsureOverlay().CurrentCredits = ev.Credits;
    }

    private void OnEnemyCount(FSEnemyCountEvent ev)
    {
        var overlay = EnsureOverlay();
        overlay.EnemiesAlive = ev.Alive;
        overlay.EnemiesTotal = ev.Total;
    }

    private void OnPerksState(FSPerksStateEvent ev)
    {
        var overlay = EnsureOverlay();
        overlay.ActiveSlots   = ev.Slots;
        overlay.PerkLevels = ev.Levels;
    }

    private void OnPrepTimer(FSPrepTimerUpdateEvent ev)
    {
        var overlay = EnsureOverlay();
        overlay.IsPrepPhase = ev.IsPrepPhase;
        overlay.PrepSecondsRemaining = ev.SecondsRemaining;
    }

    private void OnPhaseChanged(WavePhaseChangedEvent ev)
    {
        EnsureOverlay().IsReadyUpVisible = ev.IsPrepPhase;
    }

    private void OnReadyUpState(FSReadyUpStateEvent ev)
    {
        var overlay = EnsureOverlay();
        overlay.ReadyUpCount         = ev.ReadyCount;
        overlay.ReadyUpTotal         = ev.TotalCount;
        overlay.ReadyUpPlayerIsReady = ev.PlayerIsReady;
    }

    private void OnPerkStacksUpdate(FSPerkStacksUpdateEvent ev)
    {
        EnsureOverlay().PerkStacks[ev.PerkId] = ev.Stacks;
    }

    private void OnInterestPayout(FSInterestPayoutEvent ev)
    {
        EnsureOverlay().AddInterestPopup(ev.PerkId, ev.Amount);
    }

    private void OnBonusSummary(FSPlayerBonusSummaryEvent ev)
    {
        EnsureOverlay().SetBonusSummary(ev);
    }
}
