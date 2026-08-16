using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Armor.Shop;

namespace Content.Client._FinalStand.Armor;

public sealed class FSArmorShopIndicatorOverlay : FSWorldLabelOverlay<FSArmorShopComponent>
{
    protected override string Label => "ARMOR";
    protected override int FontSize => 14;
    protected override float VerticalOffset => 80f;
    protected override Color LabelColor => new(1f, 0.85f, 0f, 1f);
}
