using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Objectives;

namespace Content.Client._FinalStand.Objectives;

public sealed class FSObjectiveMarkerOverlay : FSWorldLabelOverlay<FSObjectiveMarkerComponent>
{
    protected override string Label => string.Empty;
    protected override int FontSize => 12;
    protected override float VerticalOffset => 52f;
    protected override Color LabelColor => Color.FromHex("#FFD75E");
    protected override bool DynamicLabel => true;

    protected override string GetLabel(EntityUid uid, FSObjectiveMarkerComponent marker) => Loc.GetString(marker.Label);
}
