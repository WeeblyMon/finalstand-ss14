// The Build Ring: round RCD slots around a disc that shows what the hovered piece costs against what you carry.
using System.Numerics;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.RCD;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.RCD;

public sealed class FSBuildRing : FSRingControl
{
    private readonly FSRCDLook _look;
    private readonly IEntityManager _entMan;
    private readonly EntityUid? _user;
    private readonly FSEngineerOnlyRCDComponent _fs;
    private readonly Texture? _stationIcon;
    private readonly List<FSRCDEntry> _top;
    private readonly List<FSRCDEntry> _station;
    private List<FSRCDEntry> _layer;
    private UIBox2 _toggleBox;

    public event Action<FSRCDEntry>? OnPick;
    public event Action? OnToggleLayout;

    protected override int SlotCount => _layer.Count;

    public FSBuildRing(FSRCDLook look, IEntityManager entMan, EntityUid? user, FSEngineerOnlyRCDComponent fs,
        List<FSRCDEntry> top, List<FSRCDEntry> station, Texture? stationIcon, Vector2 cursor)
        : base(top.Count, top.FindIndex(e => e.Selected), cursor)
    {
        _look = look;
        _entMan = entMan;
        _user = user;
        _fs = fs;
        _top = top;
        _station = station;
        _stationIcon = stationIcon;
        _layer = top;
    }

    protected override void Clicked(int index)
    {
        var entry = _layer[index];
        switch (entry.Kind)
        {
            case FSRCDEntryKind.Station:
                _layer = _station;
                Hot = -1;
                break;
            case FSRCDEntryKind.Back:
                _layer = _top;
                Hot = -1;
                break;
            default:
                OnPick?.Invoke(entry);
                break;
        }
    }

    protected override bool ClickedElsewhere(Vector2 pos)
    {
        if (!_toggleBox.Contains(pos))
            return false;

        OnToggleLayout?.Invoke();
        return true;
    }

    protected override void DrawSlot(DrawingHandleScreen handle, int index, Vector2 pos, bool hot, float scale)
    {
        var entry = _layer[index];
        var r = SlotRadius * scale * (hot ? HoverGrow : 1f);
        var decon = entry.Kind == FSRCDEntryKind.Deconstruct;
        var capped = entry.Cap is { } cap && cap.Used >= cap.Max;
        var edge = entry.Selected ? (decon ? FSRCDLook.Bad : FSRCDLook.Gold)
            : hot ? FSRCDLook.Fg
            : decon ? FSRCDLook.DeconEdge
            : FSRCDLook.Edge;
        Color? glow = entry.Selected ? (decon ? FSRCDLook.Bad : FSRCDLook.Gold) : null;
        var icon = entry.Kind == FSRCDEntryKind.Station ? _stationIcon : entry.Icon;

        DrawSlotBase(handle, pos, r, decon ? FSRCDLook.DeconBack : FSRCDLook.Panel, edge, glow, icon, capped || entry.Locked ? 0.4f : 1f, scale, hot);

        if (entry.Kind == FSRCDEntryKind.Back)
        {
            var font = _look.Mono(8.5f, scale);
            var text = Loc.GetString("fs-rcd-belt-back");
            handle.DrawString(font, pos - handle.GetDimensions(font, text, 1f) / 2f, text, FSRCDLook.Muted);
        }

        if (entry.Locked)
            _look.DrawLock(handle, pos + new Vector2(r * 0.62f, r * 0.62f), 16f * scale);

        if (entry.Short)
            DrawDot(handle, pos, r, scale, FSRCDLook.Bad);

        if (entry.Cap is { } badge)
            DrawBadge(handle, _look.Mono(7f, scale), pos, r, scale, $"{badge.Used}/{badge.Max}",
                badge.Used >= badge.Max ? FSRCDLook.Bad : FSRCDLook.Muted);
    }

    protected override void DrawCenter(DrawingHandleScreen handle, Vector2 center, float disc, float scale)
    {
        var entry = Hot >= 0 && Hot < _layer.Count ? _layer[Hot] : _layer.Find(e => e.Selected);
        if (entry == null)
            return;

        var width = disc * 1.55f;
        switch (entry.Kind)
        {
            case FSRCDEntryKind.Station:
                DrawTitle(handle, center, scale, width, "fs-rcd-ring-station", "fs-rcd-role-station", FSRCDLook.Fg);
                return;
            case FSRCDEntryKind.Back:
                DrawTitle(handle, center, scale, width, "fs-rcd-belt-back", "fs-rcd-role-back", FSRCDLook.Fg);
                return;
            case FSRCDEntryKind.Deconstruct:
                DrawTitle(handle, center, scale, width, "fs-rcd-role-deconstruct-title", "fs-rcd-role-deconstruct", FSRCDLook.Bad);
                return;
        }

        if (entry.Piece is not { } piece)
            return;

        var height = _look.MeasureInfo(handle, piece, scale, width);
        _look.DrawInfo(handle, _entMan, _user, _fs, piece, new Vector2(center.X, center.Y - height / 2f), scale, centered: true, width);
    }

    private void DrawTitle(DrawingHandleScreen handle, Vector2 center, float scale, float width, string title, string role, Color color)
    {
        var bold = _look.Bold(10f, scale);
        var body = _look.Body(8f, scale);
        var titleText = Loc.GetString(title);
        var roleText = Loc.GetString(role);
        var h = handle.GetDimensions(bold, titleText, 1f).Y + FSRCDLook.Wrapped(handle, body, roleText, 0, 0, width, FSRCDLook.Muted, true, draw: false);
        var y = center.Y - h / 2f;
        y += FSRCDLook.Text(handle, bold, titleText, center.X, y, color, true);
        FSRCDLook.Wrapped(handle, body, roleText, center.X, y, width, FSRCDLook.Muted, true);
    }

    protected override void DrawExtras(DrawingHandleScreen handle, Vector2 center, float scale)
    {
        var font = _look.Mono(8f, scale);
        var text = Loc.GetString("fs-rcd-switch-to-belt");
        var size = handle.GetDimensions(font, text, 1f);
        var pad = new Vector2(8f, 4f) * scale;
        var top = center.Y + (Radius + SlotRadius + 14f) * scale;
        _toggleBox = UIBox2.FromDimensions(new Vector2(center.X - size.X / 2f - pad.X, top), size + pad * 2f);
        handle.DrawRect(_toggleBox, FSRCDLook.PanelDeep);
        handle.DrawRect(_toggleBox, _toggleBox.Contains(Mouse) ? FSRCDLook.Gold : FSRCDLook.Edge, filled: false);
        handle.DrawString(font, _toggleBox.TopLeft + pad, text, FSRCDLook.Fg);
    }
}
