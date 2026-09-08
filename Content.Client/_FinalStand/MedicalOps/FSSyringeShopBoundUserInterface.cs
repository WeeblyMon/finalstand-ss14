using Content.Client._FinalStand.MedicalOps.UI;
using Content.Shared._FinalStand.MedicalOps.Shop;
using Robust.Client.UserInterface;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSSyringeShopBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private SyringeShopWindow? _window;

    private FSSyringeShopState? _lastState;

    public FSSyringeShopBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindowCenteredLeft<SyringeShopWindow>();
        _window.OnBuyPressed += OnBuyPressed;
        _window.OnClose += Close;

        if (_lastState != null)
            _window.Populate(_lastState);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is not FSSyringeShopState state)
            return;

        _lastState = state;
        _window?.Populate(state);
    }

    private void OnBuyPressed(string tierId)
    {
        SendPredictedMessage(new FSSyringeShopBuyMessage(tierId));
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
