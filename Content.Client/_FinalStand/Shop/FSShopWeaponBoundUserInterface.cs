using Content.Client._FinalStand.Shop.UI;
using Content.Shared._FinalStand.Shop;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Shop;

public sealed class FSShopWeaponBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private WeaponShopWindow? _window;

    private Action? _onCreditsChanged;
    private Action? _onUpgradesChanged;
    private Action? _onRefreshNeeded;
    private Action? _onSellCompleted;
    private Action<string>? _onSellFailed;

    public FSShopWeaponBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindowCenteredLeft<WeaponShopWindow>();
        _window.OnBuyPressed += OnBuyPressed;
        _window.OnUpgradePressed += OnUpgradePressed;
        _window.OnSellConfirmed += OnSellConfirmed;
        _window.OnClose += Close;
        _window.Populate(Owner, EntMan);

        var shopClient = EntMan.System<FSShopClientSystem>();
        _onCreditsChanged  = OnCreditsChanged;
        _onUpgradesChanged = OnUpgradesChanged;
        _onRefreshNeeded   = OnRefreshNeeded;
        _onSellCompleted = OnSellCompleted;
        _onSellFailed = OnSellFailed;
        shopClient.CreditsChanged       += _onCreditsChanged;
        shopClient.UpgradeLevelsChanged += _onUpgradesChanged;
        shopClient.RefreshNeeded        += _onRefreshNeeded;
        shopClient.SellCompleted        += _onSellCompleted;
        shopClient.SellFailed           += _onSellFailed;

        UpdateSellButtonState();
    }

    private void OnBuyPressed()
    {
        SendPredictedMessage(new FSShopBuyMessage());
    }

    private void OnUpgradePressed(string upgradeId)
    {
        SendPredictedMessage(new FSShopUpgradeMessage(upgradeId));
    }

    private void OnSellConfirmed()
    {
        SendPredictedMessage(new FSShopSellMessage());
    }

    private void OnSellCompleted()
    {
        _window?.ResetConfirmation();
        UpdateSellButtonState();
    }

    private void OnSellFailed(string _)
    {
        _window?.ResetConfirmation();
    }

    private void UpdateSellButtonState()
    {
        if (_window == null || !EntMan.TryGetComponent<FSShopWeaponComponent>(Owner, out var comp))
            return;
        var shopClient = EntMan.System<FSShopClientSystem>();
        var refund = ComputeEstimatedRefund(comp, shopClient.UpgradeLevels);
        var hasWeapon = shopClient.PlayerHasWeaponInInventory(shopClient.GetLocalPlayer(), comp.WeaponProtoId);
        _window.UpdateSellButton(refund, hasWeapon);
    }

    private static int ComputeEstimatedRefund(FSShopWeaponComponent comp, Dictionary<string, int> levels)
    {
        var estimatedSpent = 0;
        foreach (var def in comp.Upgrades)
        {
            var level = levels.GetValueOrDefault(def.Id, 0);
            estimatedSpent += def.BaseCost * level * (level + 1) / 2;
        }
        var raw = comp.Price * 0.40 + estimatedSpent * 0.40;
        return Math.Max(0, (int)Math.Round(raw / 50.0) * 50);
    }

    private void OnRefreshNeeded()
    {
        SendPredictedMessage(new FSShopRefreshMessage());
        _window?.ResetConfirmation();
        UpdateSellButtonState();
    }

    private void OnCreditsChanged()
    {
        if (_window == null)
            return;
        var shopClient = EntMan.System<FSShopClientSystem>();
        _window.UpdateBalance(shopClient.CurrentCredits);
        UpdateSellButtonState();
    }

    private void OnUpgradesChanged()
    {
        if (_window == null || !EntMan.TryGetComponent<FSShopWeaponComponent>(Owner, out var comp))
            return;
        var shopClient = EntMan.System<FSShopClientSystem>();
        _window.RefreshUpgrades(comp.Upgrades, shopClient.UpgradeLevels, shopClient.CurrentCredits);
        _window.UpdateBalance(shopClient.CurrentCredits);
        _window.UpdateWeaponTitle(shopClient.WeaponTitle);
        _window.RefreshStatBars(comp, shopClient.FindOwnedWeapon(comp.WeaponProtoId), EntMan);
        _window.ResetConfirmation();
        UpdateSellButtonState();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        var shopClient = EntMan.System<FSShopClientSystem>();
        if (_onCreditsChanged  != null) shopClient.CreditsChanged       -= _onCreditsChanged;
        if (_onUpgradesChanged != null) shopClient.UpgradeLevelsChanged -= _onUpgradesChanged;
        if (_onRefreshNeeded   != null) shopClient.RefreshNeeded        -= _onRefreshNeeded;
        if (_onSellCompleted   != null) shopClient.SellCompleted        -= _onSellCompleted;
        if (_onSellFailed      != null) shopClient.SellFailed           -= _onSellFailed;

        if (_window == null)
            return;
        _window.OnBuyPressed     -= OnBuyPressed;
        _window.OnUpgradePressed -= OnUpgradePressed;
        _window.OnSellConfirmed  -= OnSellConfirmed;
        _window.OnClose          -= Close;
        _window.Dispose();
    }
}
