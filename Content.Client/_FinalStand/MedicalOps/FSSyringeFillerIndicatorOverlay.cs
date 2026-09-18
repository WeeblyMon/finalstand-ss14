using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.MedicalOps;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSSyringeFillerIndicatorOverlay : FSWorldLabelOverlay<FSSyringeFillerComponent>
{
    protected override string Label => "SYRINGE FILLER";
    protected override int FontSize => 14;
    protected override float VerticalOffset => 80f;
    protected override Color LabelColor => new(0.62f, 0.85f, 0.95f, 1f);
}
