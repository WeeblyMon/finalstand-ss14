using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._FinalStand.Teleportation;
using Content.Shared.Teleportation.Components;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._FinalStand.Teleportation;

public sealed partial class FSHandTeleporterStatusControl : PollingItemStatusControl<FSHandTeleporterStatusControl.Data>
{
    [Dependency] private IEntityManager _entMan = default!;

    private readonly Entity<HandTeleporterComponent> _parent;
    private readonly FSExpiringPortalSystem _portals;
    private readonly RichTextLabel _label;

    public FSHandTeleporterStatusControl(Entity<HandTeleporterComponent> parent)
    {
        IoCManager.InjectDependencies(this);
        _parent = parent;
        _portals = _entMan.System<FSExpiringPortalSystem>();
        _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };
        AddChild(_label);
        Update(PollData());
    }

    protected override Data PollData()
    {
        var comp = _parent.Comp;
        var open = (comp.FirstPortal != null ? 1 : 0) + (comp.SecondPortal != null ? 1 : 0);
        var seconds = comp.PortalsExpireAt is { } at ? _portals.SecondsLeft(at) : -1;
        return new Data(open, seconds);
    }

    protected override void Update(in Data data)
    {
        var state = data.Open switch
        {
            0 => Loc.GetString("fs-hand-teleporter-status-none"),
            1 => Loc.GetString("fs-hand-teleporter-status-one"),
            _ => Loc.GetString("fs-hand-teleporter-status-linked"),
        };

        if (data.Open > 0 && data.Seconds >= 0)
        {
            var color = data.Seconds <= 5 ? "red" : data.Seconds <= 10 ? "orange" : "white";
            state += " " + Loc.GetString("fs-hand-teleporter-status-closes", ("color", color), ("seconds", data.Seconds));
        }

        _label.SetMarkup(state);
    }

    public readonly record struct Data(int Open, int Seconds);
}
