using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.Perks;
using Content.Client._FinalStand.Profile;
using Content.Client.Lobby.UI;
using Robust.Client.UserInterface.Controllers;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Leveling;

namespace Content.Client.Lobby;

public sealed partial class LobbyUIController : IOnSystemChanged<FSUiEventRelaySystem>
{
    private int _FSPerkPoints;
    private int _fsLevel = 1;
    private int _fsPrestige;
    private int _fsExperience;
    private int _fsXpToNext = 500;
    private int _fsWave;
    private WavePhase _fsWavePhase;

    public void OnSystemLoaded(FSUiEventRelaySystem system)
    {
        system.WalletUpdated += OnFSWalletUpdated;
        system.LevelingUpdated += OnFSLevelingUpdated;
        system.WaveStatusUpdated += OnFSWaveStatus;
    }

    public void OnSystemUnloaded(FSUiEventRelaySystem system)
    {
        system.WalletUpdated -= OnFSWalletUpdated;
        system.LevelingUpdated -= OnFSLevelingUpdated;
        system.WaveStatusUpdated -= OnFSWaveStatus;
    }

    private void OnFSWaveStatus(FSWaveStatusEvent ev)
    {
        _fsWave = ev.Wave;
        _fsWavePhase = ev.Phase;
        UpdateFSWaveStatus();
    }

    private void UpdateFSWaveStatus()
    {
        if (_stateManager.CurrentState is not LobbyState { Lobby: { } lobby })
            return;

        lobby.WaveStatus.Visible = _fsWave > 0;
        lobby.WaveStatus.Text = Loc.GetString(
            _fsWavePhase == WavePhase.Combat ? "fs-lobby-wave-combat" : "fs-lobby-wave-prep",
            ("wave", _fsWave));
    }

    private void OnFSWalletUpdated(WalletUpdatedEvent ev)
    {
        _FSPerkPoints = ev.PerkPoints;
        UpdateFSPerkPoints();
    }

    private void OnFSLevelingUpdated(FSLevelingUpdatedEvent ev)
    {
        _fsLevel = ev.Level;
        _fsPrestige = ev.PrestigeLevel;
        _fsExperience = ev.Experience;
        _fsXpToNext = ev.XpToNextLevel;
        UpdateFSLevelDisplay();
    }

    private void UpdateFSPerkPoints()
    {
        HookLobbyButtons();
        PreviewPanel?.SetPerkPointsText(_FSPerkPoints);
    }

    // The wallet is only pushed on request, and the lobby panel may not exist yet when the reply
    // lands. Re-ask on every lobby entry and re-apply what we already hold, so points earned last
    // round show without needing a reconnect.
    private void RefreshFinalStandLobby()
    {
        EntityManager.EntityNetManager?.SendSystemNetworkMessage(new WalletRequestEvent());
        EntityManager.EntityNetManager?.SendSystemNetworkMessage(new FSLevelingRequestMessage());
        EntityManager.EntityNetManager?.SendSystemNetworkMessage(new FSWaveStatusRequestEvent());
        UIManager.GetUIController<FSProfileUIController>().RequestTitles();
        UpdateFSPerkPoints();
        UpdateFSLevelDisplay();
        UpdateFSWaveStatus();
    }

    // new panel instance = new lobby entry, so re-wire buttons
    private LobbyCharacterPreviewPanel? _hookedPanel;

    private void HookLobbyButtons()
    {
        var panel = PreviewPanel;
        if (panel == null || panel == _hookedPanel) return;
        _hookedPanel = panel;

        panel.PrestigeButtonControl.OnPressed += _ =>
            EntityManager.EntityNetManager?.SendSystemNetworkMessage(new FSPrestigeRequestMessage());

        panel.PerkShopButtonControl.OnPressed += _ =>
            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<FSPerkShopSystem>().OpenWindow();

        var profile = UIManager.GetUIController<FSProfileUIController>();
        panel.TitlesButtonControl.OnPressed += _ => profile.OpenTitles();
        panel.MentorButtonControl.OnPressed += _ => profile.OpenMentor();

        if (!_fsTitleHooked)
        {
            _fsTitleHooked = true;
            profile.EquippedTitleChanged += UpdateFSTitle;
        }
        UpdateFSTitle();
    }

    private bool _fsTitleHooked;

    private void UpdateFSTitle()
    {
        var (name, color) = UIManager.GetUIController<FSProfileUIController>().EquippedTitleDisplay();
        PreviewPanel?.SetTitle(name == null ? null : Loc.GetString("fs-badge-leaderboard-title", ("title", name)), color);
    }

    private void UpdateFSLevelDisplay()
    {
        HookLobbyButtons();
        if (PreviewPanel == null) return;

        var levelText = _fsPrestige > 0 ? $"{_fsPrestige}-{_fsLevel}" : $"LVL {_fsLevel}";
        PreviewPanel.SetLevelText(levelText);
        PreviewPanel.SetXpText($"{_fsExperience:N0} / {_fsXpToNext:N0} XP");
        PreviewPanel.SetPrestigeButtonVisible(_fsLevel >= 50);
    }
}
