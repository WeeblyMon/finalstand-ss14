// The Build Belt: a card that explains the selected piece over a row of slots, each showing how many you can build now.
using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Shared._FinalStand.RCD;
using Content.Shared.Stacks;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.RCD;

public sealed class FSBuildBelt : Control
{
    private const float SlotW = 54f;
    private const float SlotH = 62f;
    private const float Gap = 4f;
    private const float Pad = 5f;
    private const float CardH = 104f;
    private const float Raise = 4f;
    private const float Grow = 64f / 38f;
    private const float HintWidth = 150f;
    private float _grow = Grow;
    private float _cardH = CardH;

    private readonly FSRCDLook _look;
    private readonly List<(UIBox2 Box, FSRCDEntry Entry)> _hits = new();
    private UIBox2 _toggleBox;
    private Vector2 _mouse;

    public List<FSRCDEntry> Entries = new();
    public Texture? StationIcon;
    public IReadOnlyDictionary<ProtoId<StackPrototype>, int> Carried = new Dictionary<ProtoId<StackPrototype>, int>();
    public FSEngineerOnlyRCDComponent? Rcd;

    public event Action<FSRCDEntry>? OnPick;
    public event Action? OnToggleLayout;

    public FSBuildBelt(FSRCDLook look)
    {
        _look = look;
        MouseFilter = MouseFilterMode.Stop;
    }

    public void Resize()
    {
        var beltW = Entries.Count * SlotW + Math.Max(0, Entries.Count - 1) * Gap + Pad * 2f;
        var natural = Math.Max(beltW, 440f);
        var room = UserInterfaceManager.RootControl.Size.X - 40f;
        _grow = room > 0f ? Math.Min(Grow, room / natural) : Grow;
        SetSize = new Vector2(natural, _cardH + 6f + SlotH + Pad * 2f + Raise) * _grow;
    }

    public FSRCDEntry? Hovered()
    {
        foreach (var (box, entry) in _hits)
        {
            if (box.Contains(_mouse))
                return entry;
        }

        return null;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        _mouse = args.RelativePixelPosition;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (_toggleBox.Contains(args.RelativePixelPosition))
        {
            args.Handle();
            OnToggleLayout?.Invoke();
            return;
        }

        foreach (var (box, entry) in _hits)
        {
            if (!box.Contains(args.RelativePixelPosition))
                continue;

            args.Handle();
            OnPick?.Invoke(entry);
            return;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (Rcd == null)
            return;

        var scale = UIScale * _grow;
        var width = PixelSize.X;
        DrawCard(handle, scale, width);
        DrawSlots(handle, scale, width);
    }

    private void DrawCard(DrawingHandleScreen handle, float scale, float width)
    {
        var selected = Entries.Find(e => e.Selected);
        var hintLeft = width - HintWidth * scale - 10f * scale;
        var x = 10f * scale;
        var y = 7f * scale;
        var chipFont = _look.Mono(7.5f, scale);
        var decon = selected?.Kind == FSRCDEntryKind.Deconstruct;
        var chipText = Loc.GetString(decon ? "fs-rcd-belt-chip-decon" : "fs-rcd-belt-chip-build");
        var chipSize = handle.GetDimensions(chipFont, chipText, 1f);
        var chip = UIBox2.FromDimensions(new Vector2(x, y + 1f * scale), chipSize + new Vector2(8f, 2f) * scale);

        var toggleFont = _look.Mono(8f, scale);
        var toggleText = Loc.GetString("fs-rcd-switch-to-ring");
        var toggleSize = handle.GetDimensions(toggleFont, toggleText, 1f);
        var pad = new Vector2(8f, 4f) * scale;
        _toggleBox = UIBox2.FromDimensions(new Vector2(width - toggleSize.X - pad.X * 2f - 8f * scale, 8f * scale), toggleSize + pad * 2f);
        var infoWidth = Math.Min(_toggleBox.Left, hintLeft) - chip.Right - 24f * scale;

        if (selected?.Piece is { } measured)
        {
            var needed = MathF.Ceiling((y + _look.MeasureInfo(handle, measured, scale, infoWidth, Rcd!.Locked.Contains(measured.ID)) + 8f * scale) / scale);
            needed = Math.Max(CardH, needed);
            if (!needed.Equals(_cardH))
            {
                _cardH = needed;
                Resize();
            }
        }

        var card = new UIBox2(0, 0, width, _cardH * scale);
        handle.DrawRect(card, FSRCDLook.PanelDeep);
        handle.DrawRect(card, FSRCDLook.Edge, filled: false);

        handle.DrawRect(_toggleBox, FSRCDLook.Panel);
        handle.DrawRect(_toggleBox, _toggleBox.Contains(_mouse) ? FSRCDLook.Gold : FSRCDLook.Edge, filled: false);
        handle.DrawString(toggleFont, _toggleBox.TopLeft + pad, toggleText, FSRCDLook.Fg);

        handle.DrawRect(chip, decon ? FSRCDLook.Bad : FSRCDLook.Gold);
        handle.DrawString(chipFont, chip.TopLeft + new Vector2(4f, 1f) * scale, chipText, decon ? FSRCDLook.Fg : FSPalette.PanelDeep.WithAlpha(1f));

        if (selected?.Piece is { } piece)
            _look.DrawInfo(handle, Carried, Rcd!, piece, new Vector2(chip.Right + 8f * scale, y), scale, centered: false, infoWidth);

        var hintFont = _look.Mono(7f, scale);
        var hintY = _toggleBox.Bottom + 6f * scale;
        foreach (var line in Loc.GetString("fs-rcd-belt-hint").Split(" · "))
            hintY += FSRCDLook.Wrapped(handle, hintFont, line, hintLeft, hintY, HintWidth * scale, FSRCDLook.Muted, false);
    }

    private void DrawSlots(DrawingHandleScreen handle, float scale, float width)
    {
        _hits.Clear();
        var beltW = (Entries.Count * SlotW + Math.Max(0, Entries.Count - 1) * Gap + Pad * 2f) * scale;
        var top = (_cardH + 6f + Raise) * scale;
        var belt = UIBox2.FromDimensions(new Vector2((width - beltW) / 2f, top), new Vector2(beltW, (SlotH + Pad * 2f) * scale));
        handle.DrawRect(belt, FSRCDLook.PanelDeep);
        handle.DrawRect(belt, FSRCDLook.Edge, filled: false);

        var keyFont = _look.Mono(7f, scale);
        var countFont = _look.Mono(7.5f, scale);
        for (var i = 0; i < Entries.Count; i++)
        {
            var entry = Entries[i];
            var decon = entry.Kind == FSRCDEntryKind.Deconstruct;
            var group = entry.Kind is FSRCDEntryKind.Station or FSRCDEntryKind.Back;
            var lift = entry.Selected ? Raise * scale : 0f;
            var pos = new Vector2(belt.Left + (Pad + i * (SlotW + Gap)) * scale, belt.Top + Pad * scale - lift);
            var box = UIBox2.FromDimensions(pos, new Vector2(SlotW, SlotH) * scale);
            _hits.Add((box, entry));

            var hover = box.Contains(_mouse);
            handle.DrawRect(box, entry.Selected ? (decon ? FSRCDLook.DeconSelected : FSRCDLook.SelectedBack) : decon ? FSRCDLook.DeconBack : FSRCDLook.Panel);
            var edge = entry.Selected ? (decon ? FSRCDLook.Bad : FSRCDLook.Gold) : hover ? FSRCDLook.Fg : FSRCDLook.Edge;
            handle.DrawRect(box, edge, filled: false);
            if (entry.Selected)
                handle.DrawRect(new UIBox2(box.TopLeft - Vector2.One, box.BottomRight + Vector2.One), edge, filled: false);

            handle.DrawString(keyFont, box.TopLeft + new Vector2(3f, 1f) * scale, (i + 1).ToString(), FSRCDLook.Muted);

            var capped = entry.Cap is { } cap && cap.Used >= cap.Max;
            var icon = entry.Kind == FSRCDEntryKind.Station ? StationIcon : entry.Icon;
            var iconSide = (group ? 30f : 38f) * scale;
            if (entry.Kind == FSRCDEntryKind.Back)
            {
                var back = Loc.GetString("fs-rcd-belt-back");
                var size = handle.GetDimensions(keyFont, back, 1f);
                handle.DrawString(keyFont, box.Center - size / 2f, back, FSRCDLook.Muted);
            }
            else if (icon != null)
            {
                var iconBox = UIBox2.FromDimensions(new Vector2(box.Center.X - iconSide / 2f, box.Top + 8f * scale), new Vector2(iconSide));
                handle.DrawTextureRect(icon, iconBox, Color.White.WithAlpha(capped || entry.Locked ? 0.4f : 1f));
                if (entry.Locked)
                    _look.DrawLock(handle, iconBox.Center, 18f * scale);
            }

            if (entry.Kind == FSRCDEntryKind.Station)
            {
                var label = Loc.GetString("fs-rcd-belt-station");
                var size = handle.GetDimensions(keyFont, label, 1f);
                handle.DrawString(keyFont, new Vector2(box.Center.X - size.X / 2f, box.Bottom - 18f * scale), label, FSRCDLook.Muted);
            }

            if (entry.Kind == FSRCDEntryKind.Piece && entry.Affordable >= 0)
            {
                var count = $"×{entry.Affordable}";
                var size = handle.GetDimensions(countFont, count, 1f);
                handle.DrawString(countFont, new Vector2(box.Right - size.X - 3f * scale, box.Top + 1f * scale), count,
                    entry.Affordable > 0 ? FSRCDLook.Good : FSRCDLook.Bad);
            }

            var barColor = group ? FSRCDLook.Muted : decon || entry.Short || capped ? FSRCDLook.Bad : FSRCDLook.Good;
            handle.DrawRect(new UIBox2(box.Left + 4f * scale, box.Bottom - 8f * scale, box.Right - 4f * scale, box.Bottom - 4f * scale), barColor);
        }
    }
}
