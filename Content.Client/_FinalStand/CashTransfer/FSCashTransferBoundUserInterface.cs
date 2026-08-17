using Content.Client._FinalStand.CashTransfer.UI;
using Content.Shared._FinalStand.CashTransfer;
using Robust.Client.UserInterface;

namespace Content.Client._FinalStand.CashTransfer;

public sealed class FSCashTransferBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private CashTransferWindow? _window;

    public FSCashTransferBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        if (_window != null)
            return;

        _window = this.CreateWindow<CashTransferWindow>();
        _window.OnTransferConfirmed += OnTransferConfirmed;
        _window.OnClose += Close;
    }

    private void OnTransferConfirmed(int amount)
    {
        SendMessage(new FSCashTransferRequestMessage(amount));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is FSCashTransferBuiState s)
            _window?.UpdateState(s);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        if (_window == null)
            return;

        _window.OnTransferConfirmed -= OnTransferConfirmed;
        _window.OnClose -= Close;
        _window.Dispose();
        _window = null;
    }
}
