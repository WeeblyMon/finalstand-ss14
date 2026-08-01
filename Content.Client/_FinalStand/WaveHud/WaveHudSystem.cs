using Content.Shared._FinalStand.Leveling;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.ReadyCheck;
using Content.Shared._FinalStand.WaveHud;
using Robust.Client;
using Robust.Client.Graphics;
using Robust.Shared.Player;

namespace Content.Client._FinalStand.WaveHud;

public sealed class WaveHudSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IBaseClient _client = default!;

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
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
        _client.PlayerJoinedServer += OnPlayerJoinedServer;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _client.PlayerJoinedServer -= OnPlayerJoinedServer;

        if (_overlay != null)
        {
            _overlay.OnReadyUpClicked -= SendReadyRequest;
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
        _overlayManager.AddOverlay(_overlay);
        return _overlay;
    }

    private void SendReadyRequest(bool isReady)
    {
        RaiseNetworkEvent(new FSReadyUpRequestMessage(isReady));
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
        var overlay = EnsureOverlay();
        overlay.GunDamage = ev.GunDamage;
        overlay.FireRate = ev.FireRate;
        overlay.MeleeDamage = ev.MeleeDamage;
        overlay.ExplosiveDamage = ev.ExplosiveDamage;
        overlay.ReloadSpeed = ev.ReloadSpeed;
        overlay.MagazineSize = ev.MagazineSize;
    }
}
