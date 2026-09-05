using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Deployables;

namespace Content.Client._FinalStand.Deployables;

public sealed class FSSentryTurretLabelOverlay : FSWorldLabelOverlay<FSSentryTurretComponent>
{
    protected override string Label => "0";
    protected override int FontSize => 8;
    protected override float VerticalOffset => 46f;
    protected override Color LabelColor => Color.FromHex("#FFB870");
    protected override bool DynamicLabel => true;
    protected override bool ShowArrow => false;
    protected override bool Bob => false;

    protected override bool ShouldDraw(EntityUid uid, FSSentryTurretComponent turret, TransformComponent xform)
        => xform.Anchored;

    protected override string GetLabel(EntityUid uid, FSSentryTurretComponent turret)
        => $"{turret.Ammo}/{turret.MaxAmmo}";
}
