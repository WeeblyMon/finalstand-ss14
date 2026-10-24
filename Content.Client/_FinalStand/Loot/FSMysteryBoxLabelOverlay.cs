using System.Numerics;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Loot;

namespace Content.Client._FinalStand.Loot;

public sealed class FSMysteryBoxLabelOverlay : FSWorldLabelOverlay<FSMysteryBoxComponent>
{
    protected override string Label => "0";
    protected override int FontSize => 9;
    protected override float VerticalOffset => 44f;
    protected override Color LabelColor => Color.FromHex("#9FD8FF");
    protected override bool DynamicLabel => true;
    protected override bool ShowArrow => false;
    protected override bool Bob => false;
    protected override Vector2 WorldOffset => new(0.5f, 0f);

    protected override bool ShouldDraw(EntityUid uid, FSMysteryBoxComponent box, TransformComponent xform)
        => box.State == FSMysteryBoxState.Idle;

    protected override string GetLabel(EntityUid uid, FSMysteryBoxComponent box)
        => $"PAY {box.Price}";
}
