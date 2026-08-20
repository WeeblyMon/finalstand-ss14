using Content.Client._FinalStand.Perks;
using Content.Client.Lobby.UI;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Leveling;

namespace Content.Client.Lobby;

public sealed partial class LobbyUIController
{
    private int _FSPerkPoints;
    private int _fsLevel = 1;
    private int _fsPrestige;
    private int _fsExperience;
    private int _fsXpToNext = 500;

    private void InitializeFinalStandWallet()
    {
        SubscribeNetworkEvent<WalletUpdatedEvent>(OnFSWalletUpdated);
        SubscribeNetworkEvent<FSLevelingUpdatedEvent>(OnFSLevelingUpdated);
    }

    private void OnFSWalletUpdated(WalletUpdatedEvent ev, EntitySessionEventArgs args)
    {
        _FSPerkPoints = ev.PerkPoints;
        UpdateFSPerkPoints();
    }

    private void OnFSLevelingUpdated(FSLevelingUpdatedEvent ev, EntitySessionEventArgs args)
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
        UpdateFSPerkPoints();
        UpdateFSLevelDisplay();
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
