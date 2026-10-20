using System.Globalization;
using Content.Client._FinalStand.Interface;
using Content.Shared._FinalStand.Leveling;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.ReadyCheck;
using Content.Shared._FinalStand.Respawn;
using Content.Shared._FinalStand.WaveHud;
using Robust.Client;
using Robust.Client.Graphics;
using Content.Client._FinalStand.MedicalOps;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.GameTicking;
using Robust.Client.Player;
using Content.Client.UserInterface.Screens;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    private WaveHudOverlay? _overlay;
    private readonly List<(FSPerkTimedBuff Buff, string Label)> _perkBuffs = new();

    public override void Initialize()
    {
        base.Initialize();
        InitializeWeaponText();
        InitializeVitalsText();
        SubscribeNetworkEvent<WalletUpdatedEvent>(OnWalletUpdate);
        SubscribeNetworkEvent<FSPerksStateEvent>(OnPerksState);
        SubscribeNetworkEvent<FSReadyUpStateEvent>(OnReadyUpState);
        SubscribeNetworkEvent<FSPerkStacksUpdateEvent>(OnPerkStacksUpdate);
        SubscribeNetworkEvent<FSPerkTimedBuffsEvent>(OnPerkTimedBuffs);
        SubscribeNetworkEvent<FSPerkEarningsSummaryEvent>(OnPerkEarnings);
        SubscribeNetworkEvent<FSHealPayoutEvent>(OnHealPayout);
        SubscribeNetworkEvent<FSMedicalFundUpdatedEvent>(OnMedicalFund);
        SubscribeNetworkEvent<FSMedicalStatusEvent>(OnMedicalStatus);
        SubscribeNetworkEvent<FSPlayerBonusSummaryEvent>(OnBonusSummary);
        SubscribeNetworkEvent<FSRespawnOfferEvent>(OnRespawnOffer);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
    }

    public void FlashDirective(Color colour)
    {
        var overlay = EnsureOverlay();
        overlay.DirectiveFlashColour = colour;
        overlay.DirectiveFlash = 1f;
    }

    // The server only sends when the set changes, so labels are built here rather than on every refresh.
    private void OnPerkTimedBuffs(FSPerkTimedBuffsEvent ev)
    {
        _perkBuffs.Clear();
        foreach (var buff in ev.Buffs)
        {
            var value = buff.Percent.ToString("0.#", CultureInfo.InvariantCulture);
            _perkBuffs.Add((buff, Loc.GetString(buff.Label, ("value", value))));
        }
    }

    private void OnHealPayout(FSHealPayoutEvent ev)
        => EnsureOverlay().AddHealPayout(ev.Credits, ev.Diminished);

    private void OnMedicalFund(FSMedicalFundUpdatedEvent ev)
        => EnsureOverlay().MedicalFund = ev.Balance;

    private void OnMedicalStatus(FSMedicalStatusEvent ev)
    {
        var overlay = EnsureOverlay();
        overlay.ShowMedicalFund = ev.IsMedical;
        overlay.IsMedicalStaff = ev.IsMedical;
    }

    private static readonly TimeSpan DataRefreshInterval = TimeSpan.FromSeconds(0.05);
    private TimeSpan _nextDataRefresh;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // The HUD appears with the first wave state, or the first per-player update, whichever comes first.
        if (_overlay is not { } overlay)
        {
            var waves = EntityQueryEnumerator<FSWaveStateComponent>();
            if (!waves.MoveNext(out _))
                return;

            overlay = EnsureOverlay();
        }

        // Gathering these walks inventories and queries entities, so it runs at a fixed rate rather than every frame.
        if (_timing.RealTime >= _nextDataRefresh)
        {
            _nextDataRefresh = _timing.RealTime + DataRefreshInterval;
            UpdateCasualtyStatus(overlay);
            UpdateBuffStatus(overlay);
            UpdateHarvestStatus(overlay);
            UpdateVitals(overlay);
            UpdateWeapon(overlay);
            UpdateThrowables(overlay);
            UpdateTriage(overlay);
        }

        UpdateWaveState(overlay);
        UpdateDownedDim(overlay);
        UpdateAlertsAnchor(overlay);
        UpdateChatClearance(overlay);
    }

    // The server keeps this on the wave rule entity; timers count down against our own clock.
    private void UpdateWaveState(WaveHudOverlay overlay)
    {
        var query = EntityQueryEnumerator<FSWaveStateComponent>();
        if (!query.MoveNext(out var state))
        {
            overlay.IsPrepPhase = false;
            overlay.IsReadyUpVisible = false;
            overlay.IsDarkWave = false;
            overlay.EnemiesTotal = 0;
            return;
        }

        var prep = state.Phase == WavePhase.Prep;
        var secondsLeft = Math.Max(0f, (float) (state.PhaseEndsAt - _timing.CurTime).TotalSeconds);

        overlay.CurrentWave = state.Wave;
        overlay.IsPrepPhase = prep;
        overlay.IsReadyUpVisible = prep;
        overlay.PrepSecondsRemaining = prep ? secondsLeft : -1f;
        overlay.IsDarkWave = state.DarkWave;
        overlay.DarkWaveSecondsRemaining = state.DarkWave ? secondsLeft : 0f;
        overlay.EnemiesAlive = state.EnemiesAlive;
        overlay.EnemiesTotal = state.EnemiesTotal;
    }

    private const float DownedDim = 0.22f;

    // A round can end while you're down, leaving the offer set as the lobby becomes the active screen.
    private void UpdateDownedDim(WaveHudOverlay overlay)
    {
        if (_ui.ActiveScreen is not { } screen)
            return;

        screen.Modulate = overlay.IsRespawnOfferVisible && screen is InGameScreen
            ? Color.White.WithAlpha(DownedDim)
            : Color.White;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _perkBuffs.Clear();
        if (_overlay != null)
            _overlay.IsRespawnOfferVisible = false;
    }

    private Control? _alerts;
    private Control? _alertsScreen;

    private void UpdateAlertsAnchor(WaveHudOverlay overlay)
    {
        if (_ui.ActiveScreen is not { } screen)
            return;

        if (!ReferenceEquals(screen, _alertsScreen) || _alerts is null || _alerts.Disposed)
        {
            _alertsScreen = screen;
            _alerts = FindByName(screen, "Alerts", 0);
        }

        if (_alerts is null || FSClassicHud.Enabled)
            return;

        var lift = WaveHudOverlay.BottomBandLift + overlay.VitalsBlockHeight() + 6f;
        LayoutContainer.SetMarginTop(_alerts, -lift);
        LayoutContainer.SetMarginBottom(_alerts, -lift);
    }

    private void UpdateChatClearance(WaveHudOverlay overlay)
    {
        if (_ui.ActiveScreen is DefaultGameScreen screen)
            screen.SetRightStackLift(overlay.RightStackHeight);
    }

    private static Control? FindByName(Control parent, string name, int depth)
    {
        if (depth > 6)
            return null;

        foreach (var child in parent.Children)
        {
            if (child.Name == name)
                return child;

            if (FindByName(child, name, depth + 1) is { } found)
                return found;
        }

        return null;
    }

    private void UpdateHarvestStatus(WaveHudOverlay overlay)
    {
        overlay.HarvestStatus = null;
        overlay.HarvestCapped = false;
        overlay.HarvestStock = 0f;
        overlay.HarvestAccrued = 0f;
        overlay.HarvestCap = 0f;

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
            overlay.HarvestStock = stock;
            overlay.HarvestAccrued = satchel.AccruedThisWave;
            overlay.HarvestCap = satchel.PerWaveCap;

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

    private string? _lastDirectiveSource;

    private void UpdateBuffStatus(WaveHudOverlay overlay)
    {
        overlay.MedicalBuffs.Clear();

        var now = _timing.CurTime;
        foreach (var (buff, label) in _perkBuffs)
        {
            if (now >= buff.EndTime)
                continue;

            overlay.MedicalBuffs.Add(new WaveHudOverlay.MedicalBuffRow(
                label,
                WaveHudOverlay.PerkIconPrefix + buff.PerkId.ToLowerInvariant(),
                (int) Math.Ceiling((buff.EndTime - now).TotalSeconds),
                [Loc.GetString("fs-buff-source-perk")]));
        }

        if (_player.LocalEntity is not { } player
            || !TryComp<FSMedicalBonusComponent>(player, out var bonus))
        {
            return;
        }

        foreach (var (source, buff) in bonus.Active)
        {
            if (buff.IsExpired(now))
                continue;

            var chem = source.StartsWith(FSHealthBarSystem.ChemSourcePrefix);
            if (!chem && !CmoSources.Contains(source))
                continue;

            var label = buff.Name ?? Loc.GetString("fs-buff-status");

            var seconds = buff.EndTime is { } end
                ? (int) Math.Ceiling((end - now).TotalSeconds)
                : -1;

            if (source == DirectiveSource && _lastDirectiveSource != label)
            {
                _lastDirectiveSource = label;
                FlashDirective(FSPalette.Ok);
            }

            overlay.MedicalBuffs.Add(new WaveHudOverlay.MedicalBuffRow(
                label,
                chem ? BuffIconKey(source) : "buff_" + source,
                seconds,
                [Loc.GetString(chem ? "fs-buff-source-chem" : "fs-buff-source-cmo"), ..FSMedicalBonusText.Describe(buff.Bonuses)]));
        }
    }

    private const string DirectiveSource = "directive";
    private static readonly string[] CmoSources = [DirectiveSource, "mcp", "mobilisation", "doctrine"];

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

        if (_overlay != null)
        {
            _overlay.OnReadyUpClicked -= SendReadyRequest;
            _overlay.OnRespawnClicked -= SendRespawnRequest;
            _overlayManager.RemoveOverlay(_overlay);
            _overlay = null;
        }
    }

    // Sent on attach rather than on connect: at connect the client is still on tick 1 while the
    // server is thousands of ticks ahead, so the request arrives as a late MsgEntity and warns.
    private void OnLocalPlayerAttached(LocalPlayerAttachedEvent _)
    {
        RaiseNetworkEvent(new WalletRequestEvent());
        RaiseNetworkEvent(new FSPerkStateRequestMessage());
        RaiseNetworkEvent(new FSRespawnOfferRequestEvent());
    }

    private WaveHudOverlay EnsureOverlay()
    {
        if (_overlay != null)
            return _overlay;
        _overlay = new WaveHudOverlay();
        _overlay.OnReadyUpClicked += SendReadyRequest;
        _overlay.OnRespawnClicked += SendRespawnRequest;
        _overlay.OnThrowableCycle += OnThrowableCycle;
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

    private void OnWalletUpdate(WalletUpdatedEvent ev)
    {
        EnsureOverlay().CurrentCredits = ev.Credits;
    }

    private void OnPerksState(FSPerksStateEvent ev)
    {
        var overlay = EnsureOverlay();
        overlay.ActiveSlots   = ev.Slots;
        overlay.PerkLevels = ev.Levels;
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

    private void OnPerkEarnings(FSPerkEarningsSummaryEvent ev)
    {
        EnsureOverlay().AddEarningsPopup(ev.Entries);
    }

    private void OnBonusSummary(FSPlayerBonusSummaryEvent ev)
    {
        EnsureOverlay().SetBonusSummary(ev);
    }
}
