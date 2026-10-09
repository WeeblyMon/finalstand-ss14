using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Deployables;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Deployables;

public sealed class FSDeployableLifetimeOverlay : FSWorldLabelOverlay<FSDeployableLifetimeComponent>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    protected override string Label => "0:00";
    protected override int FontSize => 8;
    protected override float VerticalOffset => 50f;
    protected override Color LabelColor => Color.White;
    protected override bool DynamicLabel => true;
    protected override bool ShowArrow => false;
    protected override bool Bob => false;

    protected override bool ShouldDraw(EntityUid uid, FSDeployableLifetimeComponent marker, TransformComponent xform)
        => !_cfg.GetCVar(CCVars.FSEngWorldChips);

    protected override string GetLabel(EntityUid uid, FSDeployableLifetimeComponent marker)
    {
        var remaining = marker.ExpiresAt - _timing.CurTime;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        return $"{(int) remaining.TotalMinutes}:{remaining.Seconds:D2}";
    }
}
