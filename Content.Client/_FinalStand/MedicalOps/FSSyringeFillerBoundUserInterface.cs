using Content.Client._FinalStand.MedicalOps.UI;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.UserInterface;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSSyringeFillerBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private SyringeFillerWindow? _window;

    public FSSyringeFillerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        if (_window != null)
            return;

        _window = this.CreateWindow<SyringeFillerWindow>();
        _window.OnFill += () => SendMessage(new FSSyringeFillerFillMessage());
        _window.OnPurge += () => SendMessage(new FSSyringeFillerPurgeMessage());
        _window.OnEject += source => SendMessage(new FSSyringeFillerEjectMessage(source));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FSSyringeFillerBuiState s)
            _window?.UpdateState(s);
    }
}
