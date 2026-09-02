using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Weapons;

namespace Content.Client._FinalStand.Weapons;

public sealed class FSChargeMeterOverlay : FSWorldLabelOverlay<FSChargeShotComponent>
{
    private const int Segments = 10;

    protected override string Label => string.Empty;
    protected override int FontSize => 9;
    protected override float VerticalOffset => 46f;
    protected override Color LabelColor => Color.FromHex("#FFAA33");
    protected override bool DynamicLabel => true;
    protected override bool ShowArrow => false;
    protected override bool Bob => false;

    protected override string GetLabel(EntityUid uid, FSChargeShotComponent charge)
    {
        if (charge.Charge <= 0f)
            return string.Empty;

        var filled = (int) MathF.Round(charge.Charge * Segments);
        return $"[{new string('|', filled)}{new string('.', Segments - filled)}]";
    }
}
