using System.Numerics;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.MedicalOps;

// Where an Über ends: a countdown over every covered player, and a banner plus screen-edge sheen when it is you.
public sealed class FSUberOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly IGameTiming _timing;
    private readonly IPlayerManager _player;
    private readonly IResourceCache _resources;
    private readonly SharedTransformSystem _xform;

    private static readonly ResPath FontPath = new("/Fonts/NotoSans/NotoSans-Bold.ttf");
    private static readonly Color Back = new(0.04f, 0.04f, 0.05f, 0.85f);
    private static readonly Color Outline = new(0f, 0f, 0f, 0.9f);

    private const float BarWidth = 38f;
    private const float BarHeight = 4f;
    private const float HeadLift = 1.05f;
    private const int SheenSteps = 14;
    private const float SheenStep = 3f;

    private Font? _small;
    private Font? _banner;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSUberOverlay(IEntityManager entManager, IGameTiming timing, IPlayerManager player, IResourceCache resources)
    {
        _entManager = entManager;
        _timing = timing;
        _player = player;
        _resources = resources;
        _xform = entManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        var font = _resources.GetResource<FontResource>(FontPath);
        _small ??= new VectorFont(font, 9);
        _banner ??= new VectorFont(font, 15);

        var now = _timing.CurTime;
        var time = (float) _timing.RealTime.TotalSeconds;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var bounds = args.ViewportBounds;
        var local = _player.LocalEntity;

        var query = _entManager.EntityQueryEnumerator<FSUberedComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var ubered, out var xform))
        {
            if (xform.MapID != args.MapId || now >= ubered.EndTime)
                continue;

            var left = (float) (ubered.EndTime - now).TotalSeconds;
            var fraction = ubered.Duration > 0f ? Math.Clamp(left / ubered.Duration, 0f, 1f) : 0f;
            var colour = FSUberVisualSystem.IsFlickerOff(ubered.EndTime - now, time) ? Color.White : ubered.SourceColor;

            if (uid == local)
                DrawSelf(handle, bounds, left, fraction, colour, ubered.SourceColor, time);

            var anchor = Vector2.Transform(_xform.GetWorldPosition(xform) + new Vector2(0f, HeadLift), matrix);
            if (anchor.X < bounds.Left || anchor.X > bounds.Right || anchor.Y < bounds.Top || anchor.Y > bounds.Bottom)
                continue;

            DrawBar(handle, anchor, BarWidth, BarHeight, fraction, colour);
            DrawCentred(handle, _small!, $"{left:0.0}s", new Vector2(anchor.X, anchor.Y - 1f), colour, above: true);
        }
    }

    private void DrawSelf(DrawingHandleScreen handle, UIBox2i bounds, float left, float fraction, Color colour, Color source, float time)
    {
        var width = bounds.Width;
        var height = bounds.Height;
        var urgency = left < 2f ? 0.5f + 0.5f * MathF.Abs(MathF.Sin(time * 10f)) : 0.35f + 0.1f * MathF.Sin(time * 3f);

        for (var i = 0; i < SheenSteps; i++)
        {
            var alpha = urgency * (1f - i / (float) SheenSteps) * 0.35f;
            var c = source.WithAlpha(alpha);
            var o = i * SheenStep;
            handle.DrawRect(new UIBox2(bounds.Left, bounds.Top + o, bounds.Right, bounds.Top + o + SheenStep), c);
            handle.DrawRect(new UIBox2(bounds.Left, bounds.Bottom - o - SheenStep, bounds.Right, bounds.Bottom - o), c);
            handle.DrawRect(new UIBox2(bounds.Left + o, bounds.Top, bounds.Left + o + SheenStep, bounds.Bottom), c);
            handle.DrawRect(new UIBox2(bounds.Right - o - SheenStep, bounds.Top, bounds.Right - o, bounds.Bottom), c);
        }

        var centreX = bounds.Left + width / 2f;
        var top = bounds.Top + height * 0.14f;
        DrawCentred(handle, _banner!, Loc.GetString("fs-uber-banner"), new Vector2(centreX, top), colour, above: false);
        DrawBar(handle, new Vector2(centreX, top + 24f), 180f, 6f, fraction, colour);
        DrawCentred(handle, _small!, $"{left:0.0}s", new Vector2(centreX, top + 33f), colour, above: false);
    }

    private static void DrawBar(DrawingHandleScreen handle, Vector2 centre, float width, float height, float fraction, Color colour)
    {
        var leftX = centre.X - width / 2f;
        handle.DrawRect(new UIBox2(leftX - 1f, centre.Y - 1f, leftX + width + 1f, centre.Y + height + 1f), Back);
        handle.DrawRect(new UIBox2(leftX, centre.Y, leftX + width * fraction, centre.Y + height), colour);
        handle.DrawRect(new UIBox2(leftX, centre.Y, leftX + width * fraction, centre.Y + 1f), Color.White.WithAlpha(0.45f));
    }

    private static void DrawCentred(DrawingHandleScreen handle, Font font, string text, Vector2 at, Color colour, bool above)
    {
        var size = handle.GetDimensions(font, text, 1f);
        var pos = new Vector2(at.X - size.X / 2f, above ? at.Y - size.Y : at.Y);
        handle.DrawString(font, pos + new Vector2(1f, 1f), text, Outline);
        handle.DrawString(font, pos, text, colour);
    }
}
