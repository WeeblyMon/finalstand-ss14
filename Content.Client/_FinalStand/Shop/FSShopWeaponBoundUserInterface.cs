using Content.Client._FinalStand.Shop.UI;
using Content.Shared._FinalStand.Shop;
using Robust.Client.UserInterface;

namespace Content.Client._FinalStand.Shop;

public sealed class FSShopWeaponBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private WeaponShopWindow? _window;

    private Action? _onCreditsChanged;
    private Action? _onUpgradesChanged;
    private Action? _onRefreshNeeded;
    private Action? _onPerkStateChanged;

    public FSShopWeaponBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindowCenteredLeft<WeaponShopWindow>();
        _window.OnBuyPressed += OnBuyPressed;
        _window.OnUpgradePressed += OnUpgradePressed;
        _window.OnBuyPerkPressed += OnBuyPerkPressed;
        _window.OnClose += Close;
        _window.Populate(Owner, EntMan);

        var shopClient = EntMan.System<FSShopClientSystem>();
        _onCreditsChanged  = OnCreditsChanged;
        _onUpgradesChanged = OnUpgradesChanged;
        _onRefreshNeeded   = OnRefreshNeeded;
        _onPerkStateChanged = OnPerkStateChanged;
        shopClient.CreditsChanged       += _onCreditsChanged;
        shopClient.UpgradeLevelsChanged += _onUpgradesChanged;
        shopClient.RefreshNeeded        += _onRefreshNeeded;
        shopClient.PerkStateChanged     += _onPerkStateChanged;
    }

    private void OnBuyPressed()
    {
        SendPredictedMessage(new FSShopBuyMessage());
    }

    private void OnUpgradePressed(string upgradeId)
    {
        SendPredictedMessage(new FSShopUpgradeMessage(upgradeId));
    }

    private void OnBuyPerkPressed(string perkProtoId)
    {
        SendPredictedMessage(new FSShopBuyPerkMessage(perkProtoId));
    }

    private void OnRefreshNeeded()
    {
        SendPredictedMessage(new FSShopRefreshMessage());
    }

    private void OnCreditsChanged()
    {
        if (_window == null)
            return;
        var shopClient = EntMan.System<FSShopClientSystem>();
        _window.UpdateBalance(shopClient.CurrentCredits);
        // Also refresh perk affordability when credits change
        _window.RefreshPerks(Owner, EntMan, shopClient.CurrentCredits);
    }

    private void OnUpgradesChanged()
    {
        if (_window == null || !EntMan.TryGetComponent<FSShopWeaponComponent>(Owner, out var comp))
            return;
        var shopClient = EntMan.System<FSShopClientSystem>();
        _window.RefreshUpgrades(comp.Upgrades, shopClient.UpgradeLevels, shopClient.CurrentCredits);
        _window.UpdateBalance(shopClient.CurrentCredits);
        _window.UpdateWeaponTitle(shopClient.WeaponTitle);
    }

    private void OnPerkStateChanged()
    {
        if (_window == null)
            return;
        var shopClient = EntMan.System<FSShopClientSystem>();
        _window.RefreshPerks(Owner, EntMan, shopClient.CurrentCredits);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        var shopClient = EntMan.System<FSShopClientSystem>();
        if (_onCreditsChanged   != null) shopClient.CreditsChanged       -= _onCreditsChanged;
        if (_onUpgradesChanged  != null) shopClient.UpgradeLevelsChanged -= _onUpgradesChanged;
        if (_onRefreshNeeded    != null) shopClient.RefreshNeeded        -= _onRefreshNeeded;
        if (_onPerkStateChanged != null) shopClient.PerkStateChanged     -= _onPerkStateChanged;

        if (_window == null)
            return;
        _window.OnBuyPressed     -= OnBuyPressed;
        _window.OnUpgradePressed -= OnUpgradePressed;
        _window.OnBuyPerkPressed -= OnBuyPerkPressed;
        _window.OnClose          -= Close;
        _window.Dispose();
    }
}
