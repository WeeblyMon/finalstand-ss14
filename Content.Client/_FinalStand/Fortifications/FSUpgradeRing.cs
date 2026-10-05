// The hammer's upgrade picker, in the same ring style as the RCD: the disc explains the hovered upgrade and what it costs.
using System.Numerics;
using Content.Client._FinalStand.RCD;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Fortifications;
using Content.Shared.Stacks;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Fortifications;

public sealed record FSUpgradeEntry(FSUpgradePrototype Upgrade, Texture? Icon, FSUpgradeBlock Block, bool Pending, bool Short);

public sealed class FSUpgradeRing : FSRingControl
{
    private readonly FSRCDLook _look;
    private readonly IReadOnlyDictionary<ProtoId<StackPrototype>, int> _carried;
    private readonly FSUpgradeableComponent _comp;
    private readonly List<FSUpgradeEntry> _entries;

    public event Action<FSUpgradeEntry>? OnPick;

    protected override int SlotCount => _entries.Count;

    public FSUpgradeRing(FSRCDLook look, IReadOnlyDictionary<ProtoId<StackPrototype>, int> carried, FSUpgradeableComponent comp,
        List<FSUpgradeEntry> entries, Vector2 cursor)
        : base(entries.Count, entries.FindIndex(e => e.Pending), cursor)
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
        var edge = entry.Pending || owned ? FSRCDLook.Gold : hot && !blocked ? FSRCDLook.Fg : FSRCDLook.Edge;
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

        var width = disc * 1.55f;
        var lines = new List<(Font Font, string Text, Color Color, bool Wrap)>
        {
            (_look.Bold(10f, scale), Loc.GetString(entry.Upgrade.Name), FSRCDLook.Fg, false),
            (_look.Body(8f, scale), Loc.GetString(entry.Upgrade.Description), FSRCDLook.Muted, true),
        };

        var status = entry.Block switch
        {
            FSUpgradeBlock.None when entry.Pending => (Loc.GetString("fs-upgrade-ring-in-progress", ("done", _comp.Poured), ("hits", entry.Upgrade.Hits)), FSRCDLook.Gold),
            FSUpgradeBlock.None => (Loc.GetString("fs-upgrade-ring-hits", ("hits", entry.Upgrade.Hits)), FSRCDLook.Muted),
            FSUpgradeBlock.Owned => (Loc.GetString("fs-upgrade-blocked-owned"), FSRCDLook.Gold),
            _ => (Loc.GetString("fs-upgrade-blocked-" + entry.Block.ToString().ToLowerInvariant()), FSRCDLook.Bad),
        };

        var icon = 16f * scale;
        var height = 0f;
        foreach (var line in lines)
            height += line.Wrap ? FSRCDLook.Wrapped(handle, line.Font, line.Text, 0, 0, width, line.Color, true, draw: false) : line.Font.GetLineHeight(1f);
        height += entry.Upgrade.Cost.Count * (icon + 2f * scale) + _look.Mono(8f, scale).GetLineHeight(1f) + 2f * scale;

        var y = center.Y - height / 2f;
        foreach (var line in lines)
        {
            y += line.Wrap
                ? FSRCDLook.Wrapped(handle, line.Font, line.Text, center.X, y, width, line.Color, true)
                : FSRCDLook.Text(handle, line.Font, line.Text, center.X, y, line.Color, true);
        }

        foreach (var (stack, need) in entry.Upgrade.Cost)
            y += _look.DrawCostRow(handle, stack, _carried.GetValueOrDefault(stack), need, $" / {need} {_look.MaterialName(stack)}", center.X, y, scale, true);

        FSRCDLook.Text(handle, _look.Mono(8f, scale), status.Item1, center.X, y + 2f * scale, status.Item2, true);
    }
}
