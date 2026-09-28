using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._FinalStand.Teleportation;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Teleportation.Components;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._FinalStand.Teleportation;

public sealed partial class FSHandTeleporterStatusControl : PollingItemStatusControl<FSHandTeleporterStatusControl.Data>
{
    [Dependency] private IEntityManager _entMan = default!;

    private readonly Entity<HandTeleporterComponent> _parent;
    private readonly FSExpiringPortalSystem _portals;
    private readonly SharedChargesSystem _charges;
    private readonly RichTextLabel _label;

    public FSHandTeleporterStatusControl(Entity<HandTeleporterComponent> parent)
    {
        IoCManager.InjectDependencies(this);
        _parent = parent;
        _portals = _entMan.System<FSExpiringPortalSystem>();
        _charges = _entMan.System<SharedChargesSystem>();
        _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };
        AddChild(_label);
        Update(PollData());
    }

    protected override Data PollData()
    {
        var uid = _parent.Owner;
        var comp = _parent.Comp;

        int charges = 0, max = 0, nextIn = 0;
        if (_entMan.TryGetComponent<LimitedChargesComponent>(uid, out var limited))
        {
            _entMan.TryGetComponent<AutoRechargeComponent>(uid, out var recharge);
            charges = _charges.GetCurrentCharges((uid, limited, recharge));
            max = limited.MaxCharges;
            if (charges < max && recharge != null)
                nextIn = (int) Math.Ceiling(_charges.GetNextRechargeTime((uid, limited, recharge)).TotalSeconds);
        }

        var open = (comp.FirstPortal != null ? 1 : 0) + (comp.SecondPortal != null ? 1 : 0);
        var seconds = comp.PortalsExpireAt is { } at
            ? (int) Math.Ceiling(_portals.SecondsLeft(at, comp.PortalsPausedLeft))
            : -1;

        return new Data(charges, max, nextIn, open, seconds, comp.PortalsPausedLeft != null);
    }

    protected override void Update(in Data data)
    {
        var pips = new string('●', data.Charges) + new string('○', Math.Max(0, data.Max - data.Charges));
        var line1 = Loc.GetString("fs-hand-teleporter-status-charges",
            ("color", data.Charges > 0 ? "#7ec8ff" : "red"),
            ("pips", pips));
        if (data.NextIn > 0)
            line1 += " " + Loc.GetString("fs-hand-teleporter-status-next", ("seconds", data.NextIn));

        var line2 = data.Open switch
        {
            0 => Loc.GetString("fs-hand-teleporter-status-none"),
            1 => Loc.GetString("fs-hand-teleporter-status-one"),
            _ => Loc.GetString("fs-hand-teleporter-status-linked"),
        };

        if (data.Open > 0 && data.Seconds >= 0)
        {
            line2 += " " + (data.Paused
                ? Loc.GetString("fs-hand-teleporter-status-paused", ("seconds", data.Seconds))
                : Loc.GetString("fs-hand-teleporter-status-closes",
                    ("color", data.Seconds <= 5 ? "red" : data.Seconds <= 15 ? "orange" : "white"),
                    ("seconds", data.Seconds)));
        }

        _label.SetMarkup(line1 + "\n" + line2);
    }

    public readonly record struct Data(int Charges, int Max, int NextIn, int Open, int Seconds, bool Paused);
}
