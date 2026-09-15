// Draws the throwable selector above the weapon module: prev arrow, active pack, next arrow.
using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudOverlay
{
    public string? ThrowableName;
    public int ThrowableStock;
    public bool ThrowableHasChoice;

    /// <summary>Fired with -1 or +1 when an arrow is clicked.</summary>
    public event Action<int>? OnThrowableCycle;

    private UIBox2 _throwPrevBounds = new(-100, -100, -99, -99);
    private UIBox2 _throwNextBounds = new(-100, -100, -99, -99);

    private const float ThrowArrowW = 20f;
    private const float ThrowSlot = 32f;
    private const float ThrowPad = 7f;
    private const float ThrowGap = 3f;

    private static readonly Color ThrowBack = new(0.04f, 0.055f, 0.075f, 0.82f);
    private static readonly Color ThrowEdge = new(0.47f, 0.55f, 0.65f, 0.22f);
    private static readonly Color ThrowCellBack = new(0.08f, 0.11f, 0.14f, 0.7f);
    private static readonly Color ThrowText = Color.FromHex("#D8E0E8");
    private static readonly Color ThrowMuted = Color.FromHex("#7c8894");
    private static readonly Color ThrowArrowLive = Color.FromHex("#8FA1B3");
    private static readonly Color ThrowArrowDead = Color.FromHex("#3c444c");

    /// <summary>Right-aligned to the same edge as the weapon module. Returns the height used.</summary>
    private float DrawThrowables(DrawingHandleScreen screen, float rightEdge, float bottom)
    {
        // Always drawn, even with nothing carried. The row is how anyone learns the G key exists,
        // and a control that only appears once you already have the item teaches nobody.
        var name = ThrowableName ?? "none";
        var empty = ThrowableName is null;

        var labelH = _cachedLabelH;
        var rowH = MathF.Max(ThrowSlot, labelH) + ThrowPad * 2f;

        var hint = "[G] tap throw  -  hold to pick";
        var hintW = screen.GetDimensions(_labelFont!, hint, 1f).X;
        var nameText = empty ? "NONE" : $"{name.ToUpperInvariant()} x{ThrowableStock}";
        var nameW = screen.GetDimensions(_labelFont!, nameText, 1f).X;

        var cellW = MathF.Max(ThrowSlot, nameW + 8f);

        // Same width as the weapon module below it, so the right stack has one left edge.
        var right = rightEdge;
        var x = right - WeaponPanelW;
        var top = bottom - rowH;

        var box = new UIBox2(x, top, right, bottom);
        DrawPanel(screen, box, ThrowBack, ThrowEdge);

        var innerY = top + ThrowPad;
        var contentH = rowH - ThrowPad * 2f;
        var cx = x + ThrowPad;

        var arrowColor = ThrowableHasChoice ? ThrowArrowLive : ThrowArrowDead;

        _throwPrevBounds = new UIBox2(cx, innerY, cx + ThrowArrowW, innerY + contentH);
        DrawArrow(screen, _throwPrevBounds, "<", arrowColor);
        cx += ThrowArrowW + ThrowGap;

        var cell = new UIBox2(cx, innerY, cx + cellW, innerY + contentH);
        DrawRounded(screen, cell, ThrowCellBack, 2f);
        screen.DrawRect(cell, ThrowEdge, filled: false);
        var nameDims = screen.GetDimensions(_labelFont!, nameText, 1f);
        screen.DrawString(_labelFont!,
            new Vector2(cell.Left + (cellW - nameDims.X) * 0.5f, cell.Top + (contentH - nameDims.Y) * 0.5f),
            nameText, !empty && ThrowableStock > 0 ? ThrowText : ThrowMuted);
        cx += cellW + ThrowGap;

        _throwNextBounds = new UIBox2(cx, innerY, cx + ThrowArrowW, innerY + contentH);
        DrawArrow(screen, _throwNextBounds, ">", arrowColor);
        cx += ThrowArrowW + 8f;

        screen.DrawString(_labelFont!,
            new Vector2(cx, innerY + (contentH - labelH) * 0.5f), hint, ThrowMuted);

        return rowH;
    }

    private void DrawArrow(DrawingHandleScreen screen, UIBox2 box, string glyph, Color color)
    {
        DrawRounded(screen, box, ThrowCellBack, 2f);
        screen.DrawRect(box, ThrowEdge, filled: false);
        var dims = screen.GetDimensions(_labelFont!, glyph, 1f);
        screen.DrawString(_labelFont!,
            new Vector2(box.Left + (box.Width - dims.X) * 0.5f, box.Top + (box.Height - dims.Y) * 0.5f),
            glyph, color);
    }

    private void HandleThrowableClick(Vector2 mousePos)
    {
        if (!ThrowableHasChoice)
            return;

        if (_throwPrevBounds.Contains(mousePos))
            OnThrowableCycle?.Invoke(-1);
        else if (_throwNextBounds.Contains(mousePos))
            OnThrowableCycle?.Invoke(1);
    }
}
