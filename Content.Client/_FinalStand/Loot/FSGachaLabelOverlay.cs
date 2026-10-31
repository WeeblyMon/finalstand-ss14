using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Loot;

namespace Content.Client._FinalStand.Loot;

public sealed class FSGachaLabelOverlay : FSWorldLabelOverlay<FSGachaCacheComponent>
{
    protected override string Label => "0";
    protected override int FontSize => 9;
    protected override float VerticalOffset => 44f;
    protected override Color LabelColor => Color.FromHex("#FFD75E");
    protected override bool DynamicLabel => true;
    protected override bool ShowArrow => false;
    protected override bool Bob => false;

    private readonly Dictionary<int, string> _labels = new();

    protected override bool ShouldDraw(EntityUid uid, FSGachaCacheComponent cache, TransformComponent xform)
        => cache.UsesLeft > 0;

    protected override string GetLabel(EntityUid uid, FSGachaCacheComponent cache)
    {
        if (_labels.TryGetValue(cache.Price, out var label))
            return label;

        label = Loc.GetString("fs-world-label-gacha-price", ("price", cache.Price.ToString()));
        _labels[cache.Price] = label;
        return label;
    }
}
