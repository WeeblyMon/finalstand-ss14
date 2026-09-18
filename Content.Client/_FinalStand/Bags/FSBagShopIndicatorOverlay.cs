using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Bags;

namespace Content.Client._FinalStand.Bags;

public sealed class FSBagShopIndicatorOverlay : FSWorldLabelOverlay<FSBagShopComponent>
{
    protected override string Label => "BAGS";
    protected override int FontSize => 14;
    protected override float VerticalOffset => 80f;
    protected override Color LabelColor => new(0.54f, 0.65f, 0.75f, 1f);
}
