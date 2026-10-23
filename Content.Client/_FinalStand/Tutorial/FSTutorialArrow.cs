using System.Numerics;
using Content.Client._FinalStand.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Tutorial;

public sealed class FSTutorialArrow : Control
{
    private const float ArrowLength = 52f;
    private const float ArrowThickness = 36f;
    private const float BobDistance = 10f;
    private const float BobSpeed = 4f;

    private float _time;

    public FSTutorialArrow()
    {
        SetSize = new Vector2(ArrowLength + BobDistance, ArrowThickness);
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var scale = UIScale;
        var bob = (MathF.Sin(_time * BobSpeed) + 1f) * 0.5f * BobDistance * scale;
        var length = ArrowLength * scale;
        var thickness = ArrowThickness * scale;

        DrawArrow(handle, length, thickness, bob, 3f * scale, FSUiPalette.BgDeep);
        DrawArrow(handle, length, thickness, bob, 0f, FSUiPalette.Currency);
    }

    private static void DrawArrow(DrawingHandleScreen handle, float length, float thickness, float start, float grow, Color color)
    {
        var mid = thickness / 2f;
        var shaft = thickness * 0.36f;
        var headStart = start + length * 0.5f;
        var tip = start + length;

        handle.DrawRect(new UIBox2(start - grow, mid - shaft / 2f - grow, headStart + 1f, mid + shaft / 2f + grow), color);
        var head = new[]
        {
            new Vector2(headStart - grow, -grow * 1.5f),
            new Vector2(headStart - grow, thickness + grow * 1.5f),
            new Vector2(tip + grow * 1.5f, mid),
        };
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, head, color);
    }
}
