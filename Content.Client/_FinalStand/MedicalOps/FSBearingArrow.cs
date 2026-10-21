using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSBearingArrow : Control
{
    private const float Radius = 7f;
    private const float HeadLength = 0.9f;
    private const float HeadHalfWidth = 0.75f;
    private const float ShaftHalfWidth = 0.2f;

    private static readonly Vector2[] Points = new Vector2[9];

    private Vector2? _direction;

    public Color ArrowColor { get; set; } = Color.White;

    public Vector2? Direction
    {
        get => _direction;
        set
        {
            _direction = value;
            Visible = value != null;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_direction is not { } dir)
            return;

        DrawArrow(handle, PixelSize / 2f, dir, Radius * UIScale, ArrowColor);
    }

    public static void DrawArrow(DrawingHandleScreen handle, Vector2 centre, Vector2 dir, float radius, Color color)
    {
        var perp = new Vector2(-dir.Y, dir.X);
        var tip = centre + dir * radius;
        var tail = centre - dir * radius;
        var headBase = tip - dir * radius * HeadLength;
        var head = perp * radius * HeadHalfWidth;
        var shaft = perp * radius * ShaftHalfWidth;

        Points[0] = tip;
        Points[1] = headBase + head;
        Points[2] = headBase - head;
        Points[3] = tail + shaft;
        Points[4] = headBase + shaft;
        Points[5] = headBase - shaft;
        Points[6] = tail + shaft;
        Points[7] = headBase - shaft;
        Points[8] = tail - shaft;

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Points, color);
    }
}
