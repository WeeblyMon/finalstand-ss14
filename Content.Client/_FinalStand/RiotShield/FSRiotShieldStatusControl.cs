using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._FinalStand.RiotShield;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._FinalStand.RiotShield;

public sealed class FSRiotShieldStatusControl : PollingItemStatusControl<FSRiotShieldStatusControl.Data>
{
    private readonly Entity<FSRiotShieldComponent> _parent;
    private readonly RichTextLabel _label;

    public FSRiotShieldStatusControl(Entity<FSRiotShieldComponent> parent)
    {
        _parent = parent;
        _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };
        AddChild(_label);
        UpdateDraw();
    }

    protected override Data PollData()
    {
        var max = _parent.Comp.BaseDurability * _parent.Comp.DurabilityMultiplier;
        return new Data(_parent.Comp.CurrentDurability, max, _parent.Comp.IsBroken);
    }

    protected override void Update(in Data data)
    {
        if (data.Broken)
        {
            _label.SetMarkup("Shield: [color=red]BROKEN[/color]");
            return;
        }

        var pct = data.Max > 0f ? data.Current / data.Max : 0f;
        var color = pct > 0.5f ? "lime" : pct > 0.25f ? "yellow" : "red";
        _label.SetMarkup($"Shield: [color={color}]{(int)data.Current}[/color] / {(int)data.Max}");
    }

    public record struct Data(float Current, float Max, bool Broken);
}
