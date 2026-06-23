// Item-status fuel gauge for the chainsaw, displayed under the active hand like the welder's.
using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._FinalStand.Chainsaw;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._FinalStand.Chainsaw;

public sealed class FSChainsawFuelStatusControl : PollingItemStatusControl<FSChainsawFuelStatusControl.Data>
{
    private readonly Entity<FSChainsawFuelComponent> _parent;
    private readonly RichTextLabel _label;

    public FSChainsawFuelStatusControl(Entity<FSChainsawFuelComponent> parent)
    {
        _parent = parent;
        _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };
        AddChild(_label);
        UpdateDraw();
    }

    protected override Data PollData()
    {
        var max = _parent.Comp.BaseMaxFuel * _parent.Comp.MaxFuelMultiplier;
        return new Data(_parent.Comp.CurrentFuel, max);
    }

    protected override void Update(in Data data)
    {
        var color = data.Fuel <= 0f
            ? "red"
            : data.Fuel < data.MaxFuel / 4f ? "darkorange" : "orange";
        _label.SetMarkup($"Fuel: [color={color}]{(int)data.Fuel}[/color] / {(int)data.MaxFuel}");
    }

    public record struct Data(float Fuel, float MaxFuel);
}
