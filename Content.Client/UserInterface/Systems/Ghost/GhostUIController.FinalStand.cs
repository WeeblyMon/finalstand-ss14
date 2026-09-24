using Content.Client._FinalStand.CryoSleep;
using Content.Shared._FinalStand.CryoSleep;
using Robust.Shared.GameObjects;

namespace Content.Client.UserInterface.Systems.Ghost;

// FINALSTAND: ghost-bar buttons for leaving cryosleep
public sealed partial class GhostUIController
{
    private FSCryoWakeupWindow? _cryoWindow;
    private FSConfirmWindow? _cryoLobbyWindow;
    private bool _hasStoredCryoBody;
    private void SubscribeFinalStandCryo()
    {
        EntityManager.EventBus.SubscribeEvent<FSCryoStatusEvent>(EventSource.Network, this, OnCryoStatus);
        EntityManager.EventBus.SubscribeEvent<FSCryoWakeupResponseEvent>(EventSource.Network, this, OnCryoWakeupResponse);
    }

    private void UnsubscribeFinalStandCryo()
    {
        EntityManager.EventBus.UnsubscribeEvent<FSCryoStatusEvent>(EventSource.Network, this);
        EntityManager.EventBus.UnsubscribeEvent<FSCryoWakeupResponseEvent>(EventSource.Network, this);
        _hasStoredCryoBody = false;
    }

    private void RequestFinalStandCryoStatus()
    {
        _net.SendSystemNetworkMessage(new FSCryoStatusRequestEvent());
    }

    private void OnCryoStatus(FSCryoStatusEvent ev)
    {
        _hasStoredCryoBody = ev.HasStoredBody;
        UpdateGui();
    }

    private void OnCryoWakeupResponse(FSCryoWakeupResponseEvent ev)
    {
        _cryoWindow?.HandleResponse(ev.Status);

        if (ev.Status != FSReturnToBodyStatus.Success)
            return;

        _hasStoredCryoBody = false;
        UpdateGui();
    }

    private void OnCryosleepReturnPressed()
    {
        if (_cryoWindow == null)
        {
            _cryoWindow = new FSCryoWakeupWindow();
            _cryoWindow.OnAccepted += () => _net.SendSystemNetworkMessage(new FSCryoWakeupRequestEvent());
            _cryoWindow.OnClose += () => _cryoWindow = null;
        }

        _cryoWindow.ResetPrompt();
        _cryoWindow.OpenCentered();
    }

    private void OnCryoLobbyPressed()
    {
        if (_cryoLobbyWindow == null)
        {
            _cryoLobbyWindow = new FSConfirmWindow(
                Loc.GetString("fs-cryo-lobby-window-title"),
                Loc.GetString("fs-cryo-lobby-window-prompt"),
                Loc.GetString("fs-cryo-lobby-window-accept"),
                Loc.GetString("fs-cryo-lobby-window-deny"));

            _cryoLobbyWindow.OnAccepted += () => _net.SendSystemNetworkMessage(new FSCryoReturnToLobbyEvent());
            _cryoLobbyWindow.OnClose += () => _cryoLobbyWindow = null;
        }

        _cryoLobbyWindow.OpenCentered();
    }
}
