// Shared ring mechanics for the FS radial pickers: slot layout, hover, click and the dark centre disc.
// It opens shifted so a chosen slot sits under the cursor, and stays clamped on screen.
using System.Numerics;
using Content.Client._FinalStand.RCD;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._FinalStand.UI;

public abstract class FSRingControl : Control
{
    protected const float SlotRadius = 30f;
    protected const float HoverGrow = 1.13f;
    private const float Grow = 64f / 42f;
    private const float MinRadius = 112f;
    private const float SlotSpacing = 76f;

    private Vector2 _anchor;
    protected readonly float Radius;
    protected int Hot = -1;
    protected Vector2 Mouse;

    public event Action? OnDismiss;

    protected abstract int SlotCount { get; }

    protected FSRingControl(int maxSlots, int startSlot, Vector2 cursor)
    {
        Radius = MathF.Max(MinRadius, maxSlots * SlotSpacing / MathF.Tau);
        MouseFilter = MouseFilterMode.Stop;
        HorizontalExpand = true;
        VerticalExpand = true;
        LayoutContainer.SetAnchorPreset(this, LayoutContainer.LayoutPreset.Wide);

        Hot = Math.Max(0, startSlot);
        _anchor = cursor - SlotOffset(Hot, maxSlots, Grow);
    }

    protected float Scale => UIScale * Grow;

    private static float Angle(int index, int count) => -MathF.PI / 2f + index * MathF.Tau / count;

    protected Vector2 SlotOffset(int index, int count, float scale)
    {
        var a = Angle(index, Math.Max(1, count));
        return new Vector2(MathF.Cos(a), MathF.Sin(a)) * Radius * scale;
    }

    protected Vector2 Center()
    {
        var pad = (Radius + SlotRadius + 30f) * Scale;
        var size = PixelSize;
        return new Vector2(
            Math.Clamp(_anchor.X, pad, Math.Max(pad, size.X - pad)),
            Math.Clamp(_anchor.Y, pad, Math.Max(pad, size.Y - pad)));
    }

    protected float DiscRadius(float scale) => Radius * 0.66f * scale;

    protected abstract void DrawSlot(DrawingHandleScreen handle, int index, Vector2 pos, bool hot, float scale);

    protected abstract void DrawCenter(DrawingHandleScreen handle, Vector2 center, float disc, float scale);

    protected abstract void Clicked(int index);

    // Lets a subclass take a click outside the slots (a layout toggle, for example).
    protected virtual bool ClickedElsewhere(Vector2 pos) => false;

    protected virtual void DrawExtras(DrawingHandleScreen handle, Vector2 center, float scale)
    {
    }

    protected void Dismiss() => OnDismiss?.Invoke();

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        var scale = Scale;
        var center = Center();
        Mouse = args.RelativePixelPosition;
        Hot = -1;
        for (var i = 0; i < SlotCount; i++)
        {
            if ((Mouse - (center + SlotOffset(i, SlotCount, scale))).Length() <= SlotRadius * scale * HoverGrow)
                Hot = i;
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            args.Handle();
            Dismiss();
            return;
        }

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        args.Handle();
        if (ClickedElsewhere(args.RelativePixelPosition))
            return;

        if (Hot < 0 || Hot >= SlotCount)
        {
            Dismiss();
            return;
        }

        Clicked(Hot);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var scale = Scale;
        var center = Center();
        var disc = DiscRadius(scale);

        FSRCDLook.Circle(handle, center, disc, FSRCDLook.PanelDeep);
        FSRCDLook.Circle(handle, center, disc, FSRCDLook.Edge, filled: false);

        for (var i = 0; i < SlotCount; i++)
            DrawSlot(handle, i, center + SlotOffset(i, SlotCount, scale), i == Hot, scale);

        DrawCenter(handle, center, disc, scale);
        DrawExtras(handle, center, scale);
    }

    // A round slot: optional glow when selected, fill, edge, then the icon.
    protected static void DrawSlotBase(DrawingHandleScreen handle, Vector2 pos, float r, Color back, Color edge, Color? glow,
        Texture? icon, float iconAlpha, float scale, bool hot)
    {
        if (glow is { } g)
            FSRCDLook.Circle(handle, pos, r + 4f * scale, g.WithAlpha(0.3f));

        FSRCDLook.Circle(handle, pos, r, back);
        FSRCDLook.RingEdge(handle, pos, r, 2f * scale, edge);

        if (icon == null)
            return;

        var side = 42f * scale * (hot ? HoverGrow : 1f);
        handle.DrawTextureRect(icon, UIBox2.FromDimensions(pos - new Vector2(side / 2f), new Vector2(side)), Color.White.WithAlpha(iconAlpha));
    }

    protected static void DrawDot(DrawingHandleScreen handle, Vector2 pos, float r, float scale, Color color)
    {
        var dot = pos + new Vector2(r * 0.68f, -r * 0.68f);
        FSRCDLook.Circle(handle, dot, 7f * scale, FSRCDLook.Panel);
        FSRCDLook.Circle(handle, dot, 5f * scale, color);
    }

    protected static void DrawBadge(DrawingHandleScreen handle, Font font, Vector2 pos, float r, float scale, string text, Color color)
    {
        var size = handle.GetDimensions(font, text, 1f);
        var box = UIBox2.FromDimensions(new Vector2(pos.X - size.X / 2f - 4f * scale, pos.Y + r - 6f * scale),
            size + new Vector2(8f * scale, 0));
        handle.DrawRect(box, FSRCDLook.PanelDeep);
        handle.DrawRect(box, FSRCDLook.Edge, filled: false);
        handle.DrawString(font, box.TopLeft + new Vector2(4f * scale, 0), text, color);
    }
}
