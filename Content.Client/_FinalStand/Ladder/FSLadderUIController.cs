using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Network;

namespace Content.Client._FinalStand.Ladder;

public sealed class FSLadderUIController : UIController
{
    [Dependency] private IClientNetManager _net = default!;

    private FSLadderWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSOpenLadderEvent>((_, _) => Open());
        SubscribeNetworkEvent<FSLadderResponseEvent>(OnResponse);
    }

    public void Open()
    {
        if (_window == null || _window.Disposed)
        {
            _window = new FSLadderWindow();
            _window.OnSelectionChanged += Request;
        }

        if (!_window.IsOpen)
            _window.OpenCentered();
        _window.MoveToFront();
        Request(_window.Difficulty, _window.Bracket, _window.Weekly);
    }

    private void Request(FSDifficulty difficulty, FSPlayerBracket bracket, bool weekly)
    {
        if (!_net.IsConnected)
            return;

        EntityManager.EntityNetManager.SendSystemNetworkMessage(new FSLadderRequestEvent
        {
            Difficulty = difficulty,
            Bracket = bracket,
            Weekly = weekly,
        });
    }

    private void OnResponse(FSLadderResponseEvent ev, EntitySessionEventArgs _)
    {
        if (_window is { Disposed: false })
            _window.Populate(ev);
    }
}
