using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Deployables;

namespace Content.Client._FinalStand.Deployables;

public sealed partial class FSAmmoBoxLabelOverlay : FSWorldLabelOverlay<FSAmmoBoxComponent>
{
    [Dependency] private IConfigurationManager _cfg = default!;

    protected override string Label => "0";
    protected override int FontSize => 8;
    protected override float VerticalOffset => 46f;
    protected override Color LabelColor => Color.FromHex("#88CCFF");
    protected override bool DynamicLabel => true;
    protected override bool ShowArrow => false;
    protected override bool Bob => false;

    // Keyed by value rather than entity, so it stays small and never needs pruning.
    private readonly Dictionary<(int Uses, int Max, bool Private), string> _labels = new();

    protected override bool ShouldDraw(EntityUid uid, FSAmmoBoxComponent box, TransformComponent xform)
        => xform.Anchored && !_cfg.GetCVar(CCVars.FSEngWorldChips);

    protected override string GetLabel(EntityUid uid, FSAmmoBoxComponent box)
    {
        var key = (box.UsesLeft, box.MaxUses, box.Private);
        if (_labels.TryGetValue(key, out var label))
            return label;

        label = Loc.GetString(box.Private ? "fs-world-label-ammo-box-private" : "fs-world-label-ammo-box-public",
            ("uses", box.UsesLeft), ("max", box.MaxUses));
        _labels[key] = label;
        return label;
    }
}
