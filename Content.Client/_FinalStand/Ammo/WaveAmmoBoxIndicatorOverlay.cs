using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Ammo;

namespace Content.Client._FinalStand.Ammo;

public sealed class WaveAmmoBoxIndicatorOverlay : FSWorldLabelOverlay<WaveAmmoBoxTagComponent>
{
    protected override string Label => "RESUPPLY";
    protected override int FontSize => 7;
    protected override float VerticalOffset => 50f;
    protected override Color LabelColor => Color.White;
}
