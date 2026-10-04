// The Build Ring: round slots around a dark disc that shows what the hovered piece costs against what you carry.
using System.Numerics;
using Content.Shared._FinalStand.RCD;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._FinalStand.RCD;

public sealed class FSBuildRing : Control
{
    private const float MinRadius = 112f;
    private const float SlotSpacing = 76f;
    private const float SlotRadius = 30f;
    private const float HoverGrow = 1.13f;

    private readonly FSRCDLook _look;
    private readonly IEntityManager _entMan;
    private readonly EntityUid? _user;
    private readonly FSEngineerOnlyRCDComponent _fs;
    private readonly Texture? _stationIcon;

    private List<FSRCDEntry> _layer;
    private readonly List<FSRCDEntry> _top;
    private readonly List<FSRCDEntry> _station;
    private Vector2 _center;
    private int _hot = -1;
    private UIBox2 _toggleBox;
    private Vector2 _mouse;
    private readonly float _radius;

    public event Action<FSRCDEntry>? OnPick;
    public event Action? OnToggleLayout;
    public event Action? OnDismiss;

    public FSBuildRing(FSRCDLook look, IEntityManager entMan, EntityUid? user, FSEngineerOnlyRCDComponent fs,
        List<FSRCDEntry> top, List<FSRCDEntry> station, Texture? stationIcon, Vector2 cursor)
    {
        _look = look;
        _entMan = entMan;
        _user = user;
        _fs = fs;
        _top = top;
        _station = station;
        _stationIcon = stationIcon;
        _layer = _top;
        _radius = MathF.Max(MinRadius, _top.Count * SlotSpacing / MathF.Tau);
        MouseFilter = MouseFilterMode.Stop;
        HorizontalExpand = true;
        VerticalExpand = true;
        LayoutContainer.SetAnchorPreset(this, LayoutContainer.LayoutPreset.Wide);

        // Open shifted so the current piece sits under the cursor; re-picking is one click.
        var current = Math.Max(0, _top.FindIndex(e => e.Selected));
        _hot = current;
        _center = cursor - SlotOffset(current, _top.Count, 1f);
    }

    private static float Angle(int index, int count) => -MathF.PI / 2f + index * MathF.Tau / count;

    private Vector2 SlotOffset(int index, int count, float scale)
    {
        var a = Angle(index, count);
        return new Vector2(MathF.Cos(a), MathF.Sin(a)) * _radius * scale;
    }

    private Vector2 Center()
    {
        var scale = UIScale;
        var pad = (_radius + SlotRadius + 30f) * scale;
        var size = PixelSize;
        return new Vector2(
            Math.Clamp(_center.X, pad, Math.Max(pad, size.X - pad)),
            Math.Clamp(_center.Y, pad, Math.Max(pad, size.Y - pad)));
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        var scale = UIScale;
        var center = Center();
        _mouse = args.RelativePixelPosition;
        _hot = -1;
        for (var i = 0; i < _layer.Count; i++)
        {
            if ((args.RelativePixelPosition - (center + SlotOffset(i, _layer.Count, scale))).Length() <= SlotRadius * scale * HoverGrow)
                _hot = i;
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            args.Handle();
            OnDismiss?.Invoke();
            return;
        }

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        args.Handle();
        if (_toggleBox.Contains(args.RelativePixelPosition))
        {
            OnToggleLayout?.Invoke();
            return;
        }

        if (_hot < 0 || _hot >= _layer.Count)
        {
            OnDismiss?.Invoke();
            return;
        }

        var entry = _layer[_hot];
        switch (entry.Kind)
        {
            case FSRCDEntryKind.Station:
                _layer = _station;
                _hot = -1;
                break;
            case FSRCDEntryKind.Back:
                _layer = _top;
                _hot = -1;
                break;
            default:
                OnPick?.Invoke(entry);
                break;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var scale = UIScale;
        var center = Center();
        var disc = _radius * 0.66f * scale;

        FSRCDLook.Circle(handle, center, disc, FSRCDLook.PanelDeep);
        FSRCDLook.Circle(handle, center, disc, FSRCDLook.Edge, filled: false);

        for (var i = 0; i < _layer.Count; i++)
            DrawSlot(handle, _layer[i], center + SlotOffset(i, _layer.Count, scale), i == _hot, scale);

        var shown = _hot >= 0 && _hot < _layer.Count ? _layer[_hot] : _layer.Find(e => e.Selected);
        DrawDisc(handle, shown, center, disc, scale);
        DrawToggle(handle, center, scale);
    }

    private void DrawSlot(DrawingHandleScreen handle, FSRCDEntry entry, Vector2 pos, bool hot, float scale)
    {
        var r = SlotRadius * scale * (hot ? HoverGrow : 1f);
        var decon = entry.Kind == FSRCDEntryKind.Deconstruct;
        var capped = entry.Cap is { } cap && cap.Used >= cap.Max;
        var back = decon ? FSRCDLook.DeconBack : FSRCDLook.Panel;
        var edge = entry.Selected ? (decon ? FSRCDLook.Bad : FSRCDLook.Gold)
            : hot ? FSRCDLook.Fg
            : decon ? FSRCDLook.DeconEdge
            : FSRCDLook.Edge;

        if (entry.Selected)
            FSRCDLook.Circle(handle, pos, r + 4f * scale, (decon ? FSRCDLook.Bad : FSRCDLook.Gold).WithAlpha(0.3f));

        FSRCDLook.Circle(handle, pos, r, back);
        FSRCDLook.RingEdge(handle, pos, r, 2f * scale, edge);

        var alpha = capped ? 0.45f : 1f;
        var icon = entry.Kind == FSRCDEntryKind.Station ? _stationIcon : entry.Icon;
        if (entry.Kind == FSRCDEntryKind.Back)
        {
            var font = _look.Mono(8.5f, scale);
            var size = handle.GetDimensions(font, Loc.GetString("fs-rcd-belt-back"), 1f);
            handle.DrawString(font, pos - size / 2f, Loc.GetString("fs-rcd-belt-back"), FSRCDLook.Muted);
        }
        else if (icon != null)
        {
            var side = 42f * scale * (hot ? HoverGrow : 1f);
            handle.DrawTextureRect(icon, UIBox2.FromDimensions(pos - new Vector2(side / 2f), new Vector2(side)), Color.White.WithAlpha(alpha));
        }

        if (entry.Short)
        {
            var dot = pos + new Vector2(r * 0.68f, -r * 0.68f);
            FSRCDLook.Circle(handle, dot, 7f * scale, FSRCDLook.Panel);
            FSRCDLook.Circle(handle, dot, 5f * scale, FSRCDLook.Bad);
        }

        if (entry.Cap is { } badge)
        {
            var font = _look.Mono(7f, scale);
            var text = $"{badge.Used}/{badge.Max}";
            var size = handle.GetDimensions(font, text, 1f);
            var box = UIBox2.FromDimensions(new Vector2(pos.X - size.X / 2f - 4f * scale, pos.Y + r - 6f * scale),
                size + new Vector2(8f * scale, 0));
            handle.DrawRect(box, FSRCDLook.PanelDeep);
            handle.DrawRect(box, FSRCDLook.Edge, filled: false);
            handle.DrawString(font, box.TopLeft + new Vector2(4f * scale, 0), text, badge.Used >= badge.Max ? FSRCDLook.Bad : FSRCDLook.Muted);
        }
    }

    private void DrawDisc(DrawingHandleScreen handle, FSRCDEntry? entry, Vector2 center, float disc, float scale)
    {
        if (entry == null)
            return;

        switch (entry.Kind)
        {
            case FSRCDEntryKind.Station:
                DrawTitle(handle, center, scale, "fs-rcd-ring-station", "fs-rcd-role-station", FSRCDLook.Fg);
                return;
            case FSRCDEntryKind.Back:
                DrawTitle(handle, center, scale, "fs-rcd-belt-back", "fs-rcd-role-back", FSRCDLook.Fg);
                return;
            case FSRCDEntryKind.Deconstruct:
                DrawTitle(handle, center, scale, "fs-rcd-role-deconstruct-title", "fs-rcd-role-deconstruct", FSRCDLook.Bad);
                return;
        }

        if (entry.Piece is not { } piece)
            return;

        var width = disc * 1.55f;
        var height = _look.MeasureInfo(handle, piece, scale, width);
        _look.DrawInfo(handle, _entMan, _user, _fs, piece, new Vector2(center.X, center.Y - height / 2f), scale, centered: true, width);
    }

    private void DrawTitle(DrawingHandleScreen handle, Vector2 center, float scale, string title, string role, Color color)
    {
        var bold = _look.Bold(10f, scale);
        var body = _look.Body(8f, scale);
        var titleText = Loc.GetString(title);
        var roleText = Loc.GetString(role);
        var width = _radius * 0.66f * scale * 1.55f;
        var h = handle.GetDimensions(bold, titleText, 1f).Y + FSRCDLook.Wrapped(handle, body, roleText, 0, 0, width, FSRCDLook.Muted, true, draw: false);
        var y = center.Y - h / 2f;
        y += FSRCDLook.Text(handle, bold, titleText, center.X, y, color, true);
        FSRCDLook.Wrapped(handle, body, roleText, center.X, y, width, FSRCDLook.Muted, true);
    }

    private void DrawToggle(DrawingHandleScreen handle, Vector2 center, float scale)
    {
        var font = _look.Mono(8f, scale);
        var text = Loc.GetString("fs-rcd-switch-to-belt");
        var size = handle.GetDimensions(font, text, 1f);
        var pad = new Vector2(8f, 4f) * scale;
        var top = center.Y + (_radius + SlotRadius + 14f) * scale;
        _toggleBox = UIBox2.FromDimensions(new Vector2(center.X - size.X / 2f - pad.X, top), size + pad * 2f);
        handle.DrawRect(_toggleBox, FSRCDLook.PanelDeep);
        handle.DrawRect(_toggleBox, _toggleBox.Contains(_mouse) ? FSRCDLook.Gold : FSRCDLook.Edge, filled: false);
        handle.DrawString(font, _toggleBox.TopLeft + pad, text, FSRCDLook.Fg);
    }
}
