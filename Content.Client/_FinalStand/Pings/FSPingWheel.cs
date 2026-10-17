using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Pings;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

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
    private const float OpenTime = 0.14f;

    private readonly FSPingLook _look;
    private readonly Vector2 _origin;
    private readonly bool _clickToPick;
    private Vector2 _mouse;
    private float _age;
    private readonly float[] _pop = new float[Slices.Length];

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

    private static Vector2 SlotDir(int index, float offset = 0f)
    {
        var a = -MathF.PI / 2f + (index + offset) * MathF.Tau / Slices.Length;
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

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _age += args.DeltaSeconds;
        var rate = 1f - MathF.Exp(-args.DeltaSeconds * 18f);
        for (var i = 0; i < _pop.Length; i++)
            _pop[i] += ((i == Selected ? 1f : 0f) - _pop[i]) * rate;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var open = MathF.Min(1f, _age / OpenTime);
        open = 1f - (1f - open) * (1f - open) * (1f - open);
        var scale = UIScale * (0.85f + 0.15f * open);
        var center = Center();
        var inner = 48f * scale;
        var outer = (Radius + SlotRadius + 10f) * scale;
        var step = 360f / Slices.Length;

        FSShapes.Disc(handle, center, outer + 3f * scale, FSPingLook.Back.WithAlpha(0.35f * open));
        FSShapes.Ring(handle, center, outer, outer - inner, FSPingLook.Back.WithAlpha(0.72f * open));
        FSShapes.Ring(handle, center, outer, 1.5f * scale, FSPalette.ChipEdge.WithAlpha(open));
        FSShapes.Ring(handle, center, inner + 1f * scale, 1.5f * scale, FSPalette.ChipEdge.WithAlpha(open));

        for (var i = 0; i < Slices.Length; i++)
        {
            var edge = SlotDir(i, 0.5f);
            handle.DrawLine(center + edge * (inner + 4f * scale), center + edge * (outer - 4f * scale), Color.White.WithAlpha(0.08f * open));
        }

        if (Selected >= 0)
        {
            var color = FSPingLook.ColorOf(Slices[Selected].Type);
            var mid = -90f + Selected * step;
            FSShapes.Arc(handle, center, outer - 2f * scale, outer - inner - 4f * scale, mid - step / 2f + 1.5f, mid + step / 2f - 1.5f,
                color.WithAlpha(0.22f * open));
            FSShapes.Arc(handle, center, outer + 1f * scale, 4f * scale, mid - step / 2f + 1.5f, mid + step / 2f - 1.5f, color.WithAlpha(open));
            FSShapes.Arc(handle, center, inner + 6f * scale, 4f * scale, mid - 10f, mid + 10f, color.WithAlpha(open));
        }

        for (var i = 0; i < Slices.Length; i++)
        {
            var pop = _pop[i];
            var pos = center + SlotDir(i) * (Radius + 4f * pop) * scale;
            var size = SlotRadius * 2f * scale * (0.88f + 0.3f * pop);
            if (pop > 0.02f)
                FSShapes.Disc(handle, pos, size / 2f + 7f * scale, FSPingLook.ColorOf(Slices[i].Type).WithAlpha(0.3f * pop * open));
            _look.DrawMarker(handle, pos, size, Slices[i].Type, (0.6f + 0.4f * pop) * open);
        }

        DrawCentre(handle, center, inner, scale, open);
    }

    private void DrawCentre(DrawingHandleScreen handle, Vector2 center, float inner, float scale, float open)
    {
        FSShapes.Disc(handle, center, inner - 2f * scale, FSPingLook.Back.WithAlpha(0.92f * open));
        var title = _look.Font((int) MathF.Round(10 * scale));
        var small = _look.Font((int) MathF.Round(7 * scale));

        if (Selected < 0)
        {
            var head = Loc.GetString("fs-ping-wheel-title");
            var hint = Loc.GetString(_clickToPick ? "fs-ping-wheel-hint-click" : "fs-ping-wheel-hint-release");
            var headSize = handle.GetDimensions(title, head, 1f);
            var hintSize = handle.GetDimensions(small, hint, 1f);
            handle.DrawString(title, center - new Vector2(headSize.X / 2f, headSize.Y), head, FSPingLook.InfoColor.WithAlpha(open));
            handle.DrawString(small, center + new Vector2(-hintSize.X / 2f, 2f * scale), hint, FSPalette.TextMuted.WithAlpha(open));
            return;
        }

        var (type, label) = Slices[Selected];
        var color = FSPingLook.ColorOf(type);
        _look.DrawMarker(handle, center - new Vector2(0f, 9f * scale), 34f * scale, type, open);
        var text = Loc.GetString(label).ToUpperInvariant();
        var dims = handle.GetDimensions(small, text, 1f);
        handle.DrawString(small, new Vector2(center.X - dims.X / 2f, center.Y + 12f * scale), text, color.WithAlpha(open));
    }
}
