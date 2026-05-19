using Content.Client._FinalStand.CCC.UI;
using Content.Shared._FinalStand.CCC;
using Robust.Client.UserInterface;

namespace Content.Client._FinalStand.CCC;

public sealed class CCCBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private CCCWindow? _window;

    public CCCBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CCCWindow>();
        _window.OnStartWavePressed += () => SendMessage(new CCCStartWaveMessage());
        _window.OnBroadcastPressed += text => SendMessage(new CCCBroadcastMessage(text));
        _window.OnClose += Close;

        EntityUid? gridUid = null;
        if (EntMan.TryGetComponent<TransformComponent>(Owner, out var xform))
            gridUid = xform.GridUid;

        _window.InitMaps(gridUid, Owner);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not CCCBoundUserInterfaceState cccState) return;
        _window?.UpdateState(cccState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _window?.Dispose();
    }
}
