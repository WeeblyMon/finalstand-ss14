using Content.Client._FinalStand.Interface;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Ladder;

public sealed class FSLadderUIController : UIController, IOnSystemChanged<FSUiEventRelaySystem>
{
    [Dependency] private IClientNetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(0.6);

    private FSLadderWindow? _window;
    private (FSDifficulty Difficulty, FSPlayerBracket Bracket, bool Weekly)? _pending;
    private TimeSpan? _lastSent;

    public void OnSystemLoaded(FSUiEventRelaySystem system)
    {
        system.LadderOpenRequested += Open;
        system.LadderResponse += OnResponse;
    }

    public void OnSystemUnloaded(FSUiEventRelaySystem system)
    {
        system.LadderOpenRequested -= Open;
        system.LadderResponse -= OnResponse;
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
        _window.ShowLoading();
        Request(_window.Difficulty, _window.Bracket, _window.Weekly);
    }

    private void Request(FSDifficulty difficulty, FSPlayerBracket bracket, bool weekly)
    {
        _pending = (difficulty, bracket, weekly);
        TrySend();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_pending != null)
            TrySend();
    }

    private void TrySend()
    {
        if (_pending is not { } pending || _lastSent is { } last && _timing.RealTime - last < SendInterval)
            return;

        _pending = null;
        if (!_net.IsConnected)
            return;

        _lastSent = _timing.RealTime;
        EntityManager.EntityNetManager.SendSystemNetworkMessage(new FSLadderRequestEvent
        {
            Difficulty = pending.Difficulty,
            Bracket = pending.Bracket,
            Weekly = pending.Weekly,
        });
    }

    private void OnResponse(FSLadderResponseEvent ev)
    {
        if (_window is { Disposed: false })
            _window.Populate(ev);
    }
}
