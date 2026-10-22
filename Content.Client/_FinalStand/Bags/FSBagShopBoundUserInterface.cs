using Content.Client._FinalStand.Bags.UI;
using Content.Shared._FinalStand.Bags;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Bags;

public sealed class FSBagShopBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private BagShopWindow? _window;

    private FSBagShopState? _lastState;

    public FSBagShopBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindowCenteredLeft<BagShopWindow>();
        _window.OnBuyPressed += OnBuyPressed;
        _window.OnClose += Close;

        if (_lastState != null)
            _window.Populate(_lastState);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is not FSBagShopState state)
            return;

        _lastState = state;
        _window?.Populate(state);
    }

    private void OnBuyPressed(EntProtoId proto)
    {
        SendPredictedMessage(new FSBagShopBuyMessage(proto));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || _window == null)
            return;

        _window.OnBuyPressed -= OnBuyPressed;
        _window.OnClose -= Close;
        _window.Dispose();
    }
}
