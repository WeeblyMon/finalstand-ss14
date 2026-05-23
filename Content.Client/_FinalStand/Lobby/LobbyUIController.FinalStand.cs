using Content.Client._FinalStand.Augments;
using Content.Client.Lobby.UI;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Leveling;

namespace Content.Client.Lobby;

public sealed partial class LobbyUIController
{
    private int _fsAugmentPoints;
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
        _fsAugmentPoints = ev.AugmentPoints;
        UpdateFSAugmentPoints();
    }

    private void OnFSLevelingUpdated(FSLevelingUpdatedEvent ev, EntitySessionEventArgs args)
    {
        _fsLevel = ev.Level;
        _fsPrestige = ev.PrestigeLevel;
        _fsExperience = ev.Experience;
        _fsXpToNext = ev.XpToNextLevel;
        UpdateFSLevelDisplay();
    }

    private void UpdateFSAugmentPoints()
    {
        HookLobbyButtons();
        PreviewPanel?.SetAugmentPointsText(_fsAugmentPoints);
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

        panel.AugmentShopButtonControl.OnPressed += _ =>
            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<FSAugmentShopSystem>().OpenWindow();
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
