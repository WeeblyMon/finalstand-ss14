using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared.Input;
using Content.Shared._FinalStand.Leaderboard;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;

namespace Content.Client._FinalStand.Leaderboard;

public sealed class FSLeaderboardUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IClientNetManager _net = default!;

    private FSLeaderboardWindow? _window;
    private MenuButton? LeaderboardButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.LeaderboardButton;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSLeaderboardUpdateEvent>(OnLeaderboardUpdate);
    }

    public void OnStateEntered(GameplayState state)
    {
        _window = new FSLeaderboardWindow();
        _window.OnOpen += OnWindowOpen;
        _window.OnClose += OnWindowClosed;
        _input.SetInputCommand(ContentKeyFunctions.OpenFinalStandLeaderboard,
            InputCmdHandler.FromDelegate(_ => ToggleLeaderboard()));
    }

    public void OnStateExited(GameplayState state)
    {
        if (_window != null)
        {
            if (_window.IsOpen)
                SetWatching(false);

            _window.OnOpen -= OnWindowOpen;
            _window.OnClose -= OnWindowClosed;
            _window.Dispose();
            _window = null;
        }

        _input.SetInputCommand(ContentKeyFunctions.OpenFinalStandLeaderboard, null);
    }

    public void UnloadButton()
    {
        if (LeaderboardButton == null)
            return;

        LeaderboardButton.OnPressed -= ToggleButtonPressed;
    }

    public void LoadButton()
    {
        if (LeaderboardButton == null)
            return;

        LeaderboardButton.OnPressed += ToggleButtonPressed;
    }

    private void ToggleButtonPressed(BaseButton.ButtonEventArgs _) => ToggleLeaderboard();

    private void OnWindowOpen()
    {
        if (LeaderboardButton != null)
            LeaderboardButton.Pressed = true;

        SetWatching(true);
    }

    private void OnWindowClosed()
    {
        if (LeaderboardButton != null)
            LeaderboardButton.Pressed = false;

        SetWatching(false);
    }

    private void SetWatching(bool watching)
    {
        if (_net.IsConnected)
            EntityManager.EntityNetManager.SendSystemNetworkMessage(new FSLeaderboardWatchEvent(watching));
    }

    private void ToggleLeaderboard()
    {
        if (_window == null)
            return;

        if (_window.IsOpen)
        {
            _window.Close();
            return;
        }

        _window.OpenCenteredRight();
        _window.MoveToFront();
    }

    private void OnLeaderboardUpdate(FSLeaderboardUpdateEvent ev, EntitySessionEventArgs _)
    {
        if (_window is not { IsOpen: true })
            return;

        _window.Populate(ev.Entries);
    }
}
