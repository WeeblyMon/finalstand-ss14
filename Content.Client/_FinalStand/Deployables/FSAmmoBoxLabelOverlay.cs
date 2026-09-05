using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Deployables;

namespace Content.Client._FinalStand.Deployables;

public sealed class FSAmmoBoxLabelOverlay : FSWorldLabelOverlay<FSAmmoBoxComponent>
{
    protected override string Label => "0";
    protected override int FontSize => 8;
    protected override float VerticalOffset => 46f;
    protected override Color LabelColor => Color.FromHex("#88CCFF");
    protected override bool DynamicLabel => true;
    protected override bool ShowArrow => false;
    protected override bool Bob => false;

    protected override bool ShouldDraw(EntityUid uid, FSAmmoBoxComponent box, TransformComponent xform)
        => xform.Anchored;

    protected override string GetLabel(EntityUid uid, FSAmmoBoxComponent box)
    {
        var access = box.Private ? "PRIVATE" : "PUBLIC";
        return $"{box.UsesLeft}/{box.MaxUses} {access}";
    }
}
