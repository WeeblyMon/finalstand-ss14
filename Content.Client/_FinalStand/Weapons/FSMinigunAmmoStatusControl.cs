using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._FinalStand.Weapons;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._FinalStand.Weapons;

public sealed class FSMinigunAmmoStatusControl : PollingItemStatusControl<FSMinigunAmmoStatusControl.Data>
{
    private readonly Entity<FSMinigunComponent> _parent;
    private readonly RichTextLabel _label;

    public FSMinigunAmmoStatusControl(Entity<FSMinigunComponent> parent)
    {
        _parent = parent;
        _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };
        AddChild(_label);
        UpdateDraw();
    }

    protected override Data PollData() => new(_parent.Comp.CurrentAmmo, _parent.Comp.MaxAmmo);

    protected override void Update(in Data data)
    {
        var color = data.Current == 0
            ? "red"
            : data.Current <= data.Max / 10
                ? "darkorange"
                : "white";
        _label.SetMarkup($"[color={color}]{data.Current}[/color] / {data.Max}");
    }

    public record struct Data(int Current, int Max);
}
