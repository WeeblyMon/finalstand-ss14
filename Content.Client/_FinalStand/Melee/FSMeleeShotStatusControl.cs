using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._FinalStand.Melee;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Melee;

// The shots a Shot heavy has ready, and a bar filling toward the next.
public sealed partial class FSMeleeShotStatusControl : PollingItemStatusControl<FSMeleeShotStatusControl.Data>
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly Color Ready = Color.FromHex("#f0b040");

    private readonly Entity<FSMeleeShotStateComponent> _parent;
    private readonly RichTextLabel _label;
    private readonly ProgressBar _bar;

    public FSMeleeShotStatusControl(Entity<FSMeleeShotStateComponent> parent)
    {
        IoCManager.InjectDependencies(this);
        _parent = parent;
        _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };
        _bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            MinHeight = 4,
            HorizontalExpand = true,
            BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#2a2a2a") },
            ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Ready },
        };

        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        box.AddChild(_label);
        box.AddChild(_bar);
        AddChild(box);
        Update(PollData());
    }

    protected override Data PollData()
    {
        var shots = _parent.Comp;
        var recharge = _entMan.TryGetComponent<FSMeleeJuiceComponent>(_parent, out var juice) ? juice.ShotRecharge : 0f;
        var left = shots.Charges < shots.MaxCharges ? (float) (shots.NextCharge - _timing.CurTime).TotalSeconds : 0f;
        var fill = recharge > 0f && left > 0f ? 1f - Math.Clamp(left / recharge, 0f, 1f) : 1f;
        return new Data(shots.Charges, shots.MaxCharges, MathF.Round(fill, 2), (int) MathF.Ceiling(Math.Max(left, 0f) * 10f));
    }

    protected override void Update(in Data data)
    {
        var pips = new string('●', data.Charges) + new string('○', Math.Max(0, data.Max - data.Charges));
        var line = Loc.GetString("fs-melee-shot-status", ("color", data.Charges > 0 ? "#f0b040" : "red"), ("pips", pips));
        if (data.Tenths > 0)
            line += " " + Loc.GetString("fs-melee-shot-status-next", ("seconds", (data.Tenths / 10f).ToString("0.0")));

        _label.SetMarkup(line);
        _bar.Value = data.Fill;
    }

    public record struct Data(int Charges, int Max, float Fill, int Tenths);
}
