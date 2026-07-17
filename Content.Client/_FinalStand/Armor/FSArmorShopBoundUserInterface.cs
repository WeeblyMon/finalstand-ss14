using Content.Client._FinalStand.Armor.UI;
using Content.Shared._FinalStand.Armor.Shop;
using Robust.Client.UserInterface;

namespace Content.Client._FinalStand.Armor;

public sealed class FSArmorShopBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ArmorShopWindow? _window;

    public FSArmorShopBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindowCenteredLeft<ArmorShopWindow>();
        _window.OnBuyPressed += OnBuyPressed;
        _window.OnClose += Close;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is FSArmorShopState armorState)
            _window?.Populate(armorState);
    }

    private void OnBuyPressed(string tierId)
    {
        SendPredictedMessage(new FSArmorShopBuyMessage(tierId));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        if (_window == null) return;
        _window.OnBuyPressed -= OnBuyPressed;
        _window.OnClose -= Close;
        _window.Dispose();
    }
}
