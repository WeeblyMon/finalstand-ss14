// The hammer's upgrade picker, in the same ring style as the RCD: the disc explains the hovered upgrade and what it costs.
using System.Linq;
using System.Numerics;
using Content.Client._FinalStand.RCD;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Fortifications;
using Content.Shared.Stacks;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Fortifications;

public sealed record FSUpgradeEntry(FSUpgradePrototype Upgrade, Texture? Icon, FSUpgradeBlock Block, bool Pending, bool Short,
    string BlockReason);

public sealed class FSUpgradeRing : FSRingControl
{
    private readonly FSRCDLook _look;
    private readonly IReadOnlyDictionary<ProtoId<StackPrototype>, int> _carried;
    private readonly FSUpgradeableComponent _comp;
    private readonly List<FSUpgradeEntry> _entries;

    public event Action<FSUpgradeEntry>? OnPick;

    private static readonly Color BranchColor = Color.FromHex("#5fb3c9");

    protected override int SlotCount => _entries.Count;

    public FSUpgradeRing(FSRCDLook look, IReadOnlyDictionary<ProtoId<StackPrototype>, int> carried, FSUpgradeableComponent comp,
        List<FSUpgradeEntry> entries, Vector2 cursor)
        : base(entries.Count, cursor)
    {
        _look = look;
        _carried = carried;
        _comp = comp;
        _entries = entries;
    }

    protected override void Clicked(int index)
    {
        var entry = _entries[index];
        if (entry.Block == FSUpgradeBlock.None)
            OnPick?.Invoke(entry);
    }

    protected override void DrawSlot(DrawingHandleScreen handle, int index, Vector2 pos, bool hot, float scale)
    {
        var entry = _entries[index];
        var r = SlotRadius * scale * (hot ? HoverGrow : 1f);
        var owned = entry.Block == FSUpgradeBlock.Owned;
        var blocked = entry.Block != FSUpgradeBlock.None;
        var branch = entry.Upgrade.Slot == FSUpgradeSlot.Branch;
        var edge = entry.Pending || owned ? FSRCDLook.Gold
            : hot && !blocked ? FSRCDLook.Fg
            : branch ? BranchColor
            : FSRCDLook.Edge;
        Color? glow = entry.Pending ? FSRCDLook.Gold : null;

        DrawSlotBase(handle, pos, r, owned ? FSRCDLook.SelectedBack : FSRCDLook.Panel, edge, glow, entry.Icon,
            blocked && !owned ? 0.4f : 1f, scale, hot);

        if (owned)
            DrawBadge(handle, _look.Mono(7f, scale), pos, r, scale, Loc.GetString("fs-upgrade-ring-fitted"), FSRCDLook.Gold);
        else if (entry.Pending)
            DrawBadge(handle, _look.Mono(7f, scale), pos, r, scale, $"{_comp.Poured}/{entry.Upgrade.Hits}", FSRCDLook.Gold);
        else if (entry.Short && !blocked)
            DrawDot(handle, pos, r, scale, FSRCDLook.Bad);
    }

    protected override void DrawUnderSlots(DrawingHandleScreen handle, Vector2 center, float scale)
    {
        var count = _entries.Count;
        var width = 4f * scale;
        for (var i = 0; i < count; i++)
        {
            if (_entries[i].Upgrade.Requires is not { } needed)
                continue;

            var j = _entries.FindIndex(e => e.Upgrade.ID == needed);
            if (j < 0)
                continue;

            var from = -90f + j * 360f / count;
            var sweep = ((-90f + i * 360f / count - from) % 360f + 540f) % 360f - 180f;
            var met = _entries[i].Block != FSUpgradeBlock.NeedsPrevious;
            var color = Hot == i || Hot == j ? FSRCDLook.Fg : met ? FSRCDLook.Gold : FSRCDLook.Muted;
            FSShapes.Arc(handle, center, Radius * scale + width / 2f, width, MathF.Min(from, from + sweep), MathF.Max(from, from + sweep), color);

            var mid = (from + sweep / 2f) * MathF.PI / 180f;
            FSShapes.Diamond(handle, center + new Vector2(MathF.Cos(mid), MathF.Sin(mid)) * Radius * scale, 12f * scale, color);
        }
    }

    protected override void DrawExtras(DrawingHandleScreen handle, Vector2 center, float scale)
    {
        var first = _entries.FindIndex(e => e.Upgrade.Slot == FSUpgradeSlot.Branch);
        var last = _entries.FindLastIndex(e => e.Upgrade.Slot == FSUpgradeSlot.Branch);
        if (first < 0 || last <= first)
            return;

        var radius = (Radius + SlotRadius + 10f) * scale;
        var from = SlotOffset(first, _entries.Count, 1f);
        var to = SlotOffset(last, _entries.Count, 1f);
        var start = MathF.Atan2(from.Y, from.X);
        var sweep = MathF.Atan2(to.Y, to.X) - start;
        if (sweep < 0f)
            sweep += MathF.Tau;

        const int steps = 24;
        var points = new Vector2[steps + 1];
        for (var w = -1f; w <= 1f; w += 1f)
        {
            for (var i = 0; i <= steps; i++)
            {
                var a = start + sweep * i / steps;
                points[i] = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (radius + w * scale);
            }

            handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, points, BranchColor.WithAlpha(0.7f));
        }

        var mid = start + sweep / 2f;
        var font = _look.Mono(8f, scale);
        var text = Loc.GetString("fs-upgrade-ring-pick-one");
        var size = handle.GetDimensions(font, text, 1f);
        var at = center + new Vector2(MathF.Cos(mid), MathF.Sin(mid)) * (radius + 6f * scale + size.Y);
        handle.DrawString(font, at - size / 2f, text, BranchColor);
    }

    protected override void DrawCenter(DrawingHandleScreen handle, Vector2 center, float disc, float scale)
    {
        var entry = Hot >= 0 && Hot < _entries.Count ? _entries[Hot] : _entries.Find(e => e.Pending);
        if (entry == null)
        {
            var hint = Loc.GetString("fs-upgrade-ring-hint");
            var body = _look.Body(8.5f, scale);
            var h = FSRCDLook.Wrapped(handle, body, hint, 0, 0, disc * 1.55f, FSRCDLook.Muted, true, draw: false);
            FSRCDLook.Wrapped(handle, body, hint, center.X, center.Y - h / 2f, disc * 1.55f, FSRCDLook.Muted, true);
            return;
        }

        var status = entry.Block switch
        {
            FSUpgradeBlock.None when entry.Pending => (Loc.GetString("fs-upgrade-ring-in-progress", ("done", _comp.Poured), ("hits", entry.Upgrade.Hits)), FSRCDLook.Gold),
            FSUpgradeBlock.None => (Loc.GetString("fs-upgrade-ring-hits", ("hits", entry.Upgrade.Hits)), FSRCDLook.Muted),
            FSUpgradeBlock.Owned => (Loc.GetString("fs-upgrade-blocked-owned"), FSRCDLook.Gold),
            _ => (entry.BlockReason, FSRCDLook.Bad),
        };

        var width = disc * 1.35f;
        var fit = scale;
        for (var shrink = 1f; shrink >= 0.7f; shrink -= 0.05f)
        {
            fit = scale * shrink;
            if (Layout(handle, entry, status, center, 0f, width, fit, draw: false) <= disc * 1.45f)
                break;
        }

        var height = Layout(handle, entry, status, center, 0f, width, fit, draw: false);
        Layout(handle, entry, status, center, center.Y - height / 2f, width, fit, draw: true);
    }

    private float Layout(DrawingHandleScreen handle, FSUpgradeEntry entry, (string Text, Color Color) status, Vector2 center,
        float top, float width, float scale, bool draw)
    {
        var y = top;
        y += FSRCDLook.Wrapped(handle, _look.Bold(10f, scale), Loc.GetString(entry.Upgrade.Name), center.X, y, width, FSRCDLook.Fg, true, draw);
        y += FSRCDLook.Wrapped(handle, _look.Body(8f, scale), Loc.GetString(entry.Upgrade.Description), center.X, y, width, FSRCDLook.Muted, true, draw);

        var costs = entry.Upgrade.Cost.ToList();
        var columns = costs.Count > 2 ? 2 : 1;
        for (var i = 0; i < costs.Count; i += columns)
        {
            var inRow = Math.Min(columns, costs.Count - i);
            for (var c = 0; c < inRow && draw; c++)
            {
                var (stack, need) = costs[i + c];
                var rest = columns == 1 ? $" / {need} {_look.MaterialName(stack)}" : $"/{need}";
                var x = inRow == 1 ? center.X : center.X + (c == 0 ? -width / 4f : width / 4f);
                _look.DrawCostRow(handle, stack, _carried.GetValueOrDefault(stack), need, rest, x, y, scale, true);
            }

            y += 18f * scale;
        }

        y += 2f * scale;
        y += FSRCDLook.Wrapped(handle, _look.Mono(8f, scale), status.Text, center.X, y, width, status.Color, true, draw);
        return y - top;
    }
}
