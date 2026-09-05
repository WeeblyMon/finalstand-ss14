using System.Numerics;
using Content.Shared._FinalStand.Weapons;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Weapons;

// Vertical charge meter drawn beside the holder, filling from the bottom as the trigger is held.
public sealed class FSChargeMeterOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float HorizontalOffset = 30f;
    private const float VerticalOffset = 0f;
    private const float BarWidth = 8f;
    private const float BarHeight = 46f;
    private const float BorderWidth = 1f;
    private const float CullMargin = 96f;
    private const int Ticks = 4;

    private static readonly Color Border = new(0f, 0f, 0f, 0.85f);
    private static readonly Color Backing = new(0.05f, 0.05f, 0.06f, 0.5f);
    private static readonly Color TickLine = new(0f, 0f, 0f, 0.45f);
    private static readonly Color LowCharge = Color.FromHex("#3FDD52");
    private static readonly Color HighCharge = Color.FromHex("#E01F1F");
    private static readonly Color FullCharge = Color.FromHex("#FFFFFF");

    private SharedTransformSystem? _xform;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSChargeMeterOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        _xform ??= _entMan.System<SharedTransformSystem>();

        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var bounds = args.ViewportBounds;

        var query = _entMan.EntityQueryEnumerator<FSChargeShotComponent, TransformComponent>();
        while (query.MoveNext(out _, out var charge, out var xform))
        {
            if (xform.MapID != args.MapId || charge.Charge <= 0.005f)
                continue;

            var screenPos = Vector2.Transform(_xform.GetWorldPosition(xform), matrix);

            if (screenPos.X < bounds.Left - CullMargin || screenPos.X > bounds.Right + CullMargin ||
                screenPos.Y < bounds.Top - CullMargin || screenPos.Y > bounds.Bottom + CullMargin)
                continue;

            DrawMeter(handle, screenPos, Math.Clamp(charge.Charge, 0f, 1f));
        }
    }

    private void DrawMeter(DrawingHandleScreen handle, Vector2 screenPos, float charge)
    {
        var left = screenPos.X + HorizontalOffset;
        var centre = screenPos.Y + VerticalOffset;
        var top = centre - BarHeight / 2f;
        var bottom = centre + BarHeight / 2f;

        handle.DrawRect(new UIBox2(
            left - BorderWidth,
            top - BorderWidth,
            left + BarWidth + BorderWidth,
            bottom + BorderWidth), Border);

        handle.DrawRect(new UIBox2(left, top, left + BarWidth, bottom), Backing);

        var fill = charge >= 1f
            ? Pulse()
            : Color.InterpolateBetween(LowCharge, HighCharge, charge);

        var fillTop = bottom - BarHeight * charge;
        handle.DrawRect(new UIBox2(left, fillTop, left + BarWidth, bottom), fill);

        for (var i = 1; i < Ticks; i++)
        {
            var y = bottom - BarHeight * i / Ticks;
            handle.DrawRect(new UIBox2(left, y, left + BarWidth, y + BorderWidth), TickLine);
        }
    }

    // Full charge breathes between hot and white so the release window is unmistakable.
    private Color Pulse()
    {
        var t = (MathF.Sin((float) _timing.CurTime.TotalSeconds * 12f) + 1f) * 0.5f;
        return Color.InterpolateBetween(HighCharge, FullCharge, t);
    }
}
