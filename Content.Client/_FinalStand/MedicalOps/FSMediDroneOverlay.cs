using System.Numerics;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSMediDroneOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly IGameTiming _timing;
    private readonly IResourceCache _resources;
    private readonly SharedTransformSystem _xform;

    private static readonly ResPath FontPath = new("/Fonts/NotoSans/NotoSans-Bold.ttf");

    private static readonly Color Shadow = new(0f, 0f, 0f, 0.28f);
    private static readonly Color BarBack = new(0.05f, 0.05f, 0.06f, 0.85f);
    private static readonly Color BarFill = Color.FromHex("#5FE3B4");
    private static readonly Color BarLow = Color.FromHex("#FF4A4A");
    private static readonly Color TextOutline = new(0f, 0f, 0f, 0.9f);

    private const float BarWidth = 30f;
    private const float BarHeight = 4f;
    private const float BarLift = 0.55f;

    private Font? _font;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV | OverlaySpace.ScreenSpace;

    public FSMediDroneOverlay(IEntityManager entManager, IGameTiming timing, IResourceCache resources)
    {
        _entManager = entManager;
        _timing = timing;
        _resources = resources;
        _xform = entManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Space == OverlaySpace.WorldSpaceBelowFOV)
            DrawShadows(args);
        else
            DrawCountdowns(args);
    }

    private void DrawShadows(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var time = (float) _timing.RealTime.TotalSeconds;

        var query = _entManager.EntityQueryEnumerator<FSMediDroneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var pos = _xform.GetWorldPosition(xform);
            if (!args.WorldAABB.Contains(pos))
                continue;

            // The shadow shrinks as the drone bobs up, which sells the hover more than the bob alone.
            var squeeze = 1f - FSMediDroneVisualSystem.Bob(uid, time) * 2f;
            handle.SetTransform(Matrix3x2.CreateScale(squeeze, 0.45f * squeeze) * Matrix3x2.CreateTranslation(pos - new Vector2(0f, 0.25f)));
            handle.DrawCircle(Vector2.Zero, 0.24f, Shadow);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawCountdowns(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        _font ??= new VectorFont(_resources.GetResource<FontResource>(FontPath), 9);

        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var time = (float) _timing.RealTime.TotalSeconds;

        var query = _entManager.EntityQueryEnumerator<FSMediDroneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var drone, out var xform))
        {
            if (xform.MapID != args.MapId || drone.MaxSeconds <= 0)
                continue;

            var lift = FSMediDroneVisualSystem.HoverHeight + FSMediDroneVisualSystem.Bob(uid, time) + BarLift;
            var anchor = Vector2.Transform(_xform.GetWorldPosition(xform) + new Vector2(0f, lift), matrix);
            var bounds = args.ViewportBounds;
            if (anchor.X < bounds.Left || anchor.X > bounds.Right || anchor.Y < bounds.Top || anchor.Y > bounds.Bottom)
                continue;

            var fraction = FSMediDroneVisualSystem.Fraction(drone);
            var fill = fraction <= FSMediDroneVisualSystem.LowCharge ? BarLow : BarFill;

            var left = anchor.X - BarWidth / 2f;
            handle.DrawRect(new UIBox2(left - 1f, anchor.Y - 1f, left + BarWidth + 1f, anchor.Y + BarHeight + 1f), BarBack);
            handle.DrawRect(new UIBox2(left, anchor.Y, left + BarWidth * fraction, anchor.Y + BarHeight), fill);

            var text = $"{drone.SecondsLeft}s";
            var size = handle.GetDimensions(_font, text, 1f);
            var textPos = new Vector2(anchor.X - size.X / 2f, anchor.Y - size.Y - 1f);
            handle.DrawString(_font, textPos + new Vector2(1f, 1f), text, TextOutline);
            handle.DrawString(_font, textPos, text, fill);
        }
    }
}
