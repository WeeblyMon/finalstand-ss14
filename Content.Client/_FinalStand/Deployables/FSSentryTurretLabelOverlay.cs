using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Deployables;
using Robust.Shared.Map;

namespace Content.Client._FinalStand.Deployables;

public sealed partial class FSSentryTurretLabelOverlay : FSWorldLabelOverlay<FSSentryTurretComponent>
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entMan = default!;

    private SharedMapSystem? _map;

    protected override string Label => "0";
    protected override int FontSize => 8;
    protected override float VerticalOffset => 46f;
    protected override Color LabelColor => Color.FromHex("#FFB870");
    protected override bool DynamicLabel => true;
    protected override bool ShowArrow => false;
    protected override bool Bob => false;

    protected override bool ShouldDraw(EntityUid uid, FSSentryTurretComponent turret, TransformComponent xform)
        => xform.Anchored && !_cfg.GetCVar(CCVars.FSEngWorldChips);

    protected override string GetLabel(EntityUid uid, FSSentryTurretComponent turret)
        => $"{turret.Ammo}/{turret.MaxAmmo}";

    protected override float Lift(EntityUid uid, FSSentryTurretComponent turret, TransformComponent xform)
    {
        _map ??= _entMan.System<SharedMapSystem>();
        return FSWorldHudSpace.BarNorth(_entMan, _map, xform) ? 16f : 0f;
    }
}
