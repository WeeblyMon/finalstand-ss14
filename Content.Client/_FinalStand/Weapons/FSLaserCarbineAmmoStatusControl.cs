using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._FinalStand.Weapons;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._FinalStand.Weapons;

public sealed class FSLaserCarbineAmmoStatusControl : PollingItemStatusControl<FSLaserCarbineAmmoStatusControl.Data>
{
    private readonly Entity<FSLaserCarbineAmmoComponent> _parent;
    private readonly RichTextLabel _label;

    public FSLaserCarbineAmmoStatusControl(Entity<FSLaserCarbineAmmoComponent> parent)
    {
        _parent = parent;
        _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };
        AddChild(_label);
        UpdateDraw();
    }

    protected override Data PollData() => new(_parent.Comp.CurrentAmmo, _parent.Comp.MaxAmmo);

    protected override void Update(in Data data)
    {
        var ratio = data.Max > 0 ? (float) data.Current / data.Max : 0f;
        var color = Color.InterpolateBetween(Color.Red, Color.White, Math.Clamp(ratio, 0f, 1f));
        _label.SetMarkup($"[color={color.ToHexNoAlpha()}]{data.Current}[/color] / {data.Max}");
    }

    public record struct Data(int Current, int Max);
}
