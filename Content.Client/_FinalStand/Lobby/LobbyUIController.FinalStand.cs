using Content.Shared._FinalStand.Economy;
using Robust.Shared.GameObjects;

namespace Content.Client.Lobby;

public sealed partial class LobbyUIController
{
    private int _fsAugmentPoints;

    private void InitializeFinalStandWallet()
    {
        SubscribeNetworkEvent<WalletUpdatedEvent>(OnFSWalletUpdated);
    }

    private void OnFSWalletUpdated(WalletUpdatedEvent ev, EntitySessionEventArgs args)
    {
        _fsAugmentPoints = ev.AugmentPoints;
        UpdateFSAugmentPoints();
    }

    private void UpdateFSAugmentPoints()
    {
        PreviewPanel?.SetAugmentPointsText(_fsAugmentPoints);
    }
}
