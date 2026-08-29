using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Weapons.Ranged.ItemStatus;

// Horizontal box that measures children right to left, so the trailing ammo counter is sized before the
// greedy bullet renderers claim the width.
public sealed class ReverseMeasureBox : BoxContainer
{
    public ReverseMeasureBox()
    {
        Orientation = LayoutOrientation.Horizontal;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var visible = 0;
        foreach (var child in Children)
        {
            if (child.Visible)
                visible++;
        }

        var separation = (SeparationOverride ?? 0) * Math.Max(0, visible - 1);
        var remaining = MathF.Max(0, availableSize.X - separation);
        var desired = new Vector2(separation, 0);

        for (var i = ChildCount - 1; i >= 0; i--)
        {
            var child = Children[i];
            if (!child.Visible)
                continue;

            child.Measure(new Vector2(remaining, availableSize.Y));
            desired.X += child.DesiredSize.X;
            desired.Y = MathF.Max(desired.Y, child.DesiredSize.Y);
            remaining = MathF.Max(0, remaining - child.DesiredSize.X);
        }

        return desired;
    }
}
