// The eight-slice ping wheel. Opened by holding the ping key it follows the mouse direction and sends on release;
// opened by the wheel key it waits for a click.
using System.Numerics;
using Content.Shared._FinalStand.Pings;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._FinalStand.Pings;

public sealed class FSPingWheel : Control
{
    public static readonly (FSPingType Type, string Label)[] Slices =
    [
        (FSPingType.Go, "fs-ping-wheel-go"),
        (FSPingType.Danger, "fs-ping-wheel-danger"),
        (FSPingType.Defend, "fs-ping-wheel-defend"),
        (FSPingType.NeedAmmo, "fs-ping-wheel-ammo"),
        (FSPingType.Acknowledge, "fs-ping-wheel-ack"),
        (FSPingType.NeedRepair, "fs-ping-wheel-repair"),
        (FSPingType.NeedChems, "fs-ping-wheel-chems"),
        (FSPingType.FallBack, "fs-ping-wheel-fallback"),
    ];

    private const float Radius = 92f;
    private const float SlotRadius = 26f;
    private const float DeadZone = 30f;

    private readonly FSPingLook _look;
    private readonly Vector2 _origin;
    private readonly bool _clickToPick;
    private Vector2 _mouse;

    public int Selected { get; private set; } = -1;
    public event Action<FSPingType>? OnPicked;
    public event Action? OnCancelled;

    public FSPingWheel(FSPingLook look, Vector2 origin, bool clickToPick)
    {
        _look = look;
        _origin = origin;
        _mouse = origin;
        _clickToPick = clickToPick;
        MouseFilter = clickToPick ? MouseFilterMode.Stop : MouseFilterMode.Ignore;
        HorizontalExpand = true;
        VerticalExpand = true;
        LayoutContainer.SetAnchorPreset(this, LayoutContainer.LayoutPreset.Wide);
    }

    private Vector2 Center()
    {
        var pad = (Radius + SlotRadius + 20f) * UIScale;
        var size = PixelSize;
        return new Vector2(
            Math.Clamp(_origin.X, pad, Math.Max(pad, size.X - pad)),
            Math.Clamp(_origin.Y, pad, Math.Max(pad, size.Y - pad)));
    }

    private static Vector2 SlotDir(int index)
    {
        var a = -MathF.PI / 2f + index * MathF.Tau / Slices.Length;
        return new Vector2(MathF.Cos(a), MathF.Sin(a));
    }

    public void Track(Vector2 mouse)
    {
        _mouse = mouse;
        var offset = mouse - Center();
        if (offset.Length() < DeadZone * UIScale)
        {
            Selected = -1;
            return;
        }

        var angle = MathF.Atan2(offset.Y, offset.X) + MathF.PI / 2f;
        var step = MathF.Tau / Slices.Length;
        Selected = (int) MathF.Round(((angle % MathF.Tau) + MathF.Tau) % MathF.Tau / step) % Slices.Length;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        Track(args.RelativePixelPosition);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (!_clickToPick)
            return;

        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            args.Handle();
            OnCancelled?.Invoke();
            return;
        }

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        args.Handle();
        Track(args.RelativePixelPosition);
        if (Selected >= 0)
            OnPicked?.Invoke(Slices[Selected].Type);
        else
            OnCancelled?.Invoke();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var scale = UIScale;
        var center = Center();
        var disc = 48f * scale;

        handle.DrawCircle(center, (Radius + SlotRadius + 8f) * scale, Color.ToSrgb(FSPingLook.Back.WithAlpha(0.55f)));
        handle.DrawCircle(center, disc, Color.ToSrgb(FSPingLook.Back));
        FSPingLook.Ring(handle, center, disc, 1.5f, Color.FromHex("#363b44"));

        for (var i = 0; i < Slices.Length; i++)
        {
            var hot = i == Selected;
            var pos = center + SlotDir(i) * Radius * scale;
            var size = SlotRadius * 2f * scale * (hot ? 1.15f : 1f);
            if (hot)
                handle.DrawCircle(pos, size / 2f + 6f * scale, Color.ToSrgb(FSPingLook.ColorOf(Slices[i].Type).WithAlpha(0.35f)));
            _look.DrawMarker(handle, pos, size, Slices[i].Type, hot ? 1f : 0.75f);
        }

        var text = Selected >= 0 ? Loc.GetString(Slices[Selected].Label) : string.Empty;
        if (text.Length == 0)
            return;

        var font = _look.Font((int) MathF.Round(11 * scale));
        var dims = handle.GetDimensions(font, text, 1f);
        handle.DrawString(font, center - dims / 2f, text, FSPingLook.ColorOf(Slices[Selected].Type));
    }
}
