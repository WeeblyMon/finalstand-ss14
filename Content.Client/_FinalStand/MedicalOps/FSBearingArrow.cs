using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSBearingArrow : Control
{
    private const float Radius = 7f;
    private const float BackSweep = 0.55f;
    private const float HalfWidth = 0.7f;

    private readonly Vector2[] _points = new Vector2[3];

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

        var centre = PixelSize / 2f;
        var scale = UIScale;

        var perp = new Vector2(-dir.Y, dir.X);
        var apex = centre + dir * Radius * scale;
        var back = centre - dir * Radius * BackSweep * scale;
        var wing = perp * Radius * HalfWidth * scale;

        _points[0] = apex;
        _points[1] = back + wing;
        _points[2] = back - wing;

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _points, ArrowColor);
    }
}
